using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using JobApplicationAgent.Documents.Application;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

try
{
    if (args.Length != 1 || args[0] is not ("pdf" or "docx")) return 2;
    using var input = Console.OpenStandardInput();
    using var bytes = new MemoryStream();
    var buffer = new byte[81920];
    int read;
    while ((read = await input.ReadAsync(buffer)) != 0)
    {
        if (bytes.Length + read > ResumeFileValidator.MaxBytes) return 2;
        bytes.Write(buffer.AsSpan(0, read));
    }
    var content = bytes.ToArray();
    ResumeFileValidator.Validate("source." + args[0], args[0] == "pdf" ? "application/pdf" : ResumeFileValidator.DocxType, content);
    var segments = new List<TextSegment>();
    long length = 0;
    void Add(string text)
    {
        length += text.Length;
        if (length > ExtractedText.MaxCharacters || segments.Count >= (args[0] == "pdf" ? 40 : 2000))
            throw new InvalidDataException();
        segments.Add(new(segments.Count + 1, text));
    }
    if (args[0] == "pdf")
    {
        using var pdf = PdfDocument.Open(content, new ParsingOptions { UseLenientParsing = false, MaxStackDepth = 128 });
        if (pdf.IsEncrypted || pdf.NumberOfPages is < 1 or > 40) return 2;
        for (var index = 1; index <= pdf.NumberOfPages; index++) Add(ContentOrderTextExtractor.GetText(pdf.GetPage(index)));
    }
    else
    {
        using var zip = new ZipArchive(new MemoryStream(content));
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = ResumeFileValidator.MaxBytes });
        var xml = XDocument.Load(reader);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = xml.Root!.Element(word + "body") ?? throw new InvalidDataException();
        foreach (var paragraph in body.Descendants(word + "p"))
        {
            // Main-body text, not a rendering. Deleted revisions, field instructions and drawings are excluded.
            var text = new StringBuilder();
            foreach (var node in paragraph.Descendants().Where(x => !x.Ancestors().Any(a =>
                         a.Name == word + "del" || a.Name == word + "drawing")))
            {
                if (node.Name == word + "t") text.Append(node.Value);
                else if (node.Name == word + "tab") text.Append('\t');
                else if (node.Name == word + "br" || node.Name == word + "cr") text.Append('\n');
            }
            Add(text.ToString());
        }
    }
    var result = new ExtractedText(ExtractedText.Version, args[0] == "pdf" ? "Page" : "Paragraph", segments.ToArray());
    result.Validate();
    Console.Write(JsonSerializer.Serialize(result));
    return 0;
}
catch
{
    // No document content or parser stack traces on stdout/stderr.
    return 2;
}
