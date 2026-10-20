using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JobApplicationAgent.Documents.Application;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.TrueType;
using UglyToad.PdfPig.Fonts.TrueType.Parser;
using UglyToad.PdfPig.Writer;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ApprovedDocumentRenderer : IApprovedDocumentRenderer
{
    public string Version => "verified-export-v1-pdfpig-0.1.16";
    private readonly SemaphoreSlim capacity = new(2, 2);
    private static readonly byte[] Font = LoadFont();
    private static byte[] LoadFont()
    {
        using var source = typeof(ApprovedDocumentRenderer).Assembly.GetManifestResourceStream("JobApplicationAgent.Documents.Infrastructure.Assets.Vera.ttf")!;
        using var buffer = new MemoryStream(); source.CopyTo(buffer); return buffer.ToArray();
    }
    public async Task<RenderedDocument> RenderAsync(ComposedDocumentContent content, string format, CancellationToken ct)
    {
        if (content?.PlainText is null || content.PlainText.Length is < 1 or > 100000 || content.PlainText.Count(x => x == '\n') > 2000 ||
            content.PlainText.Any(x => char.IsControl(x) && x != '\n'))
            throw new DraftRejectedException("Export supports 1–100000 characters and at most 2000 line breaks, without other control characters.");
        try { XmlConvert.VerifyXmlChars(content.PlainText); }
        catch (XmlException) { throw new DraftRejectedException("The approved text contains characters that cannot be represented safely."); }
        if (!await capacity.WaitAsync(0, ct)) throw new DocumentExportUnavailableException();
        try
        {
            ct.ThrowIfCancellationRequested();
            return format switch { "Pdf" => Pdf(content.PlainText, ct), "Docx" => Docx(content.PlainText, ct),
                _ => throw new InvalidResumeException("Unsupported export format.") };
        }
        finally { capacity.Release(); }
    }
    private sealed record Line(string Text, double Size, double Y);
    private static RenderedDocument Pdf(string text, CancellationToken ct)
    {
        var parsedFont = TrueTypeFontParser.Parse(new TrueTypeDataBytes(Font));
        var widths = new Dictionary<char, double>();
        foreach (var character in text.Where(x => x != '\n').Distinct())
        {
            if (!parsedFont.TableRegister.CMapTable.TryGetGlyphIndex(character, out var glyph) || glyph == 0 ||
                !parsedFont.TryGetAdvanceWidth(character, out var width))
                throw new DraftRejectedException($"The bundled PDF font cannot render U+{(int)character:X4}; no replacement glyph is exported. DOCX may support this text.");
            widths[character] = width / parsedFont.GetUnitsPerEm();
        }
        const double top = 788, bottom = 62, availableWidth = 487;
        List<List<Line>> pages = [[]]; var y = top;
        var sourceLines = text.Split('\n');
        for (var index = 0; index < sourceLines.Length; index++)
        {
            ct.ThrowIfCancellationRequested(); var source = sourceLines[index];
            var heading = index > 0 && IsHeading(source); var size = index == 0 ? 16d : heading ? 12d : 11d;
            var step = index == 0 ? 22d : 16d;
            if (heading) { if (y < bottom + 48) NewPage(); y -= 4; }
            if (source.Length == 0) { if (y >= bottom + step) y -= 12; continue; }
            var start = 0;
            while (start < source.Length)
            {
                ct.ThrowIfCancellationRequested();
                var end = start; var lastSpace = -1; var width = 0d;
                while (end < source.Length && width + widths[source[end]] * size <= availableWidth)
                { width += widths[source[end]] * size; if (source[end] == ' ') lastSpace = end; end++; }
                if (end == start) throw new DraftRejectedException("A glyph exceeds the available PDF line width.");
                if (end < source.Length && lastSpace >= start) end = lastSpace + 1;
                if (y < bottom + step) NewPage();
                pages[^1].Add(new(source[start..end], size, y)); y -= step; start = end;
            }
        }
        var builder = new PdfDocumentBuilder { IncludeDocumentInformation = false };
        var font = builder.AddTrueTypeFont(Font);
        for (var index = 0; index < pages.Count; index++)
        {
            ct.ThrowIfCancellationRequested(); var page = builder.AddPage(595.28, 841.89);
            foreach (var line in pages[index]) page.AddText(line.Text, line.Size, new PdfPoint(54, line.Y), font);
            page.AddText($"Page {index + 1}/{pages.Count}", 9, new PdfPoint(54, 30), font);
        }
        return new(builder.Build(), pages.Count);
        void NewPage()
        {
            if (pages.Count >= 40) throw new DraftRejectedException("PDF export is limited to 40 pages; prepare a smaller selection.");
            pages.Add([]); y = top;
        }
    }
    private static bool IsHeading(string line) => line is "Compétence" or "Expérience" or "Formation" or "Certification";
    private static RenderedDocument Docx(string text, CancellationToken ct)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
        var body = new XElement(w + "body"); var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            ct.ThrowIfCancellationRequested();
            var style = index == 0 ? "Title" : IsHeading(lines[index]) ? "Heading2" : "Normal";
            body.Add(new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style))),
                new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), lines[index]))));
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)),
            new XElement(w + "pgMar", new XAttribute(w + "top", 1080), new XAttribute(w + "bottom", 1080), new XAttribute(w + "left", 1080), new XAttribute(w + "right", 1080))));
        var document = new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body);
        var styles = new XElement(w + "styles", new XAttribute(XNamespace.Xmlns + "w", w),
            new XElement(w + "docDefaults", new XElement(w + "rPrDefault", new XElement(w + "rPr",
                new XElement(w + "rFonts", new XAttribute(w + "ascii", "Bitstream Vera Sans"), new XAttribute(w + "hAnsi", "Bitstream Vera Sans")),
                new XElement(w + "sz", new XAttribute(w + "val", 22)))),
                new XElement(w + "pPrDefault", new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", 0), new XAttribute(w + "line", 300), new XAttribute(w + "lineRule", "auto"))))),
            Style("Normal", "Normal", 22, false), Style("Title", "Title", 32, true), Style("Heading2", "heading 2", 24, true));
        var fontKey = "00112233445566778899AABBCCDDEEFF"; var key = Convert.FromHexString(fontKey); var font = Font.ToArray();
        for (var index = 0; index < 32; index++) font[index] ^= key[15 - index % 16];
        var fonts = new XElement(w + "fonts", new XAttribute(XNamespace.Xmlns + "w", w), new XAttribute(XNamespace.Xmlns + "r", r),
            new XElement(w + "font", new XAttribute(w + "name", "Bitstream Vera Sans"), new XElement(w + "family", new XAttribute(w + "val", "swiss")),
                new XElement(w + "embedRegular", new XAttribute(r + "id", "font1"), new XAttribute(w + "fontKey", "{00112233-4455-6677-8899-AABBCCDDEEFF}"))));
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteXml("[Content_Types].xml", new XElement(types + "Types",
                new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(types + "Default", new XAttribute("Extension", "odttf"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.obfuscatedFont")),
                Override("document", "document.main"), Override("styles", "styles"), Override("fontTable", "fontTable")));
            WriteXml("_rels/.rels", new XElement(relationships + "Relationships", Rel("doc", "officeDocument", "word/document.xml")));
            WriteXml("word/_rels/document.xml.rels", new XElement(relationships + "Relationships", Rel("styles", "styles", "styles.xml"), Rel("fonts", "fontTable", "fontTable.xml")));
            WriteXml("word/_rels/fontTable.xml.rels", new XElement(relationships + "Relationships", Rel("font1", "font", "fonts/Vera.odttf")));
            WriteXml("word/document.xml", document); WriteXml("word/styles.xml", styles); WriteXml("word/fontTable.xml", fonts);
            using var target = Entry("word/fonts/Vera.odttf").Open(); target.Write(font);
            XElement Override(string name, string type) => new(types + "Override", new XAttribute("PartName", $"/word/{name}.xml"),
                new XAttribute("ContentType", $"application/vnd.openxmlformats-officedocument.wordprocessingml.{type}+xml"));
            XElement Rel(string id, string type, string target) => new(relationships + "Relationship", new XAttribute("Id", id),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/" + type), new XAttribute("Target", target));
            ZipArchiveEntry Entry(string path)
            { var entry = zip.CreateEntry(path, CompressionLevel.Optimal); entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero); return entry; }
            void WriteXml(string path, XElement root)
            { using var target = Entry(path).Open(); using var writer = XmlWriter.Create(target, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false }); root.Save(writer); }
        }
        return new(stream.ToArray(), null);
        XElement Style(string id, string name, int size, bool keep) => new(w + "style", new XAttribute(w + "type", "paragraph"), new XAttribute(w + "styleId", id),
            id == "Normal" ? new XAttribute(w + "default", "1") : null, new XElement(w + "name", new XAttribute(w + "val", name)),
            id == "Normal" ? null : new XElement(w + "basedOn", new XAttribute(w + "val", "Normal")),
            new XElement(w + "pPr", keep ? new XElement(w + "keepNext") : null), new XElement(w + "rPr", new XElement(w + "sz", new XAttribute(w + "val", size))));
    }
}
