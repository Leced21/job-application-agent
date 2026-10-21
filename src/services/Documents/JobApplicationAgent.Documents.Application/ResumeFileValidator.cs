using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JobApplicationAgent.Documents.Application;

public static class ResumeFileValidator
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public static (string FileName, string MediaType) Validate(string name, string type, byte[] bytes)
    {
        name = name.Replace('\\', '/').Split('/').Last();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(IsUnsafeFileNameCharacter) || bytes.Length is < 1 or > MaxBytes)
            throw new InvalidResumeException("Invalid filename or content size.");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf" && type == "application/pdf")
        {
            var header = Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(8, bytes.Length)));
            var tail = Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).TrimEnd();
            if (!(header.Length == 8 && ((header.StartsWith("%PDF-1.") && header[7] is >= '0' and <= '7') || header == "%PDF-2.0")) || !tail.EndsWith("%%EOF"))
                throw new InvalidResumeException("Invalid PDF envelope.");
            // Full parsing and malware scanning are separate stages; never render an upload inline.
            return (name, "application/pdf");
        }
        if (extension != ".docx" || type != DocxType) throw new InvalidResumeException("Only matching PDF or DOCX filenames and media types are supported.");
        try { ValidateDocx(bytes); }
        catch (InvalidResumeException) { throw; }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException or ArgumentException)
        { throw new InvalidResumeException("Invalid DOCX package."); }
        return (name, DocxType);
    }
    private static bool IsUnsafeFileNameCharacter(char character) =>
        char.IsControl(character) || character is '\u061C' or '\u200E' or '\u200F' or
            >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069';

    private static void ValidateDocx(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (archive.Entries.Count is < 3 or > 200) throw new InvalidResumeException("Invalid DOCX entry count.");
        long expanded = 0;
        long actualExpanded = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        XDocument? types = null, document = null, relationships = null;
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName;
            expanded += entry.Length;
            if (!names.Add(path) || path.Contains('\\') || path.StartsWith('/') || path.Split('/').Any(x => x is ".." or ".") ||
                expanded > 20 * 1024 * 1024 || entry.Length > MaxBytes ||
                path.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || path.Contains("embeddings/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                throw new InvalidResumeException("Unsupported or oversized DOCX package.");
            var isXml = path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);
            using var source = entry.Open();
            using var stream = new MemoryStream();
            var chunk = new byte[81920];
            long actualEntryLength = 0;
            int read;
            while ((read = source.Read(chunk)) != 0)
            {
                actualEntryLength += read;
                actualExpanded += read;
                if (actualEntryLength > MaxBytes || actualExpanded > 20 * 1024 * 1024)
                    throw new InvalidResumeException("Expanded DOCX content exceeds limits.");
                if (isXml) stream.Write(chunk.AsSpan(0, read));
            }
            if (actualEntryLength != entry.Length) throw new InvalidResumeException("Invalid DOCX entry length.");
            if (!isXml) continue;
            stream.Position = 0;
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes });
            var xml = XDocument.Load(reader);
            if (xml.Descendants().Attributes().Any(x => x.Name.LocalName == "TargetMode" && x.Value.Equals("External", StringComparison.OrdinalIgnoreCase)) ||
                xml.Descendants().Attributes().Any(x => x.Name.LocalName == "ContentType" && x.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidResumeException("External relationships or macros are not supported.");
            if (path == "[Content_Types].xml") types = xml;
            if (path == "word/document.xml") document = xml;
            if (path == "_rels/.rels") relationships = xml;
        }
        XNamespace contentNs = "http://schemas.openxmlformats.org/package/2006/content-types";
        XNamespace relationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace wordNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        if (types?.Root?.Name != contentNs + "Types" || document?.Root?.Name != wordNs + "document" ||
            relationships?.Root?.Name != relationshipNs + "Relationships" ||
            !types.Descendants(contentNs + "Override").Any(x => (string?)x.Attribute("PartName") == "/word/document.xml" &&
                (string?)x.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml") ||
            !relationships.Descendants(relationshipNs + "Relationship").Any(x =>
                (string?)x.Attribute("Type") == "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" &&
                (string?)x.Attribute("Target") is "word/document.xml" or "/word/document.xml"))
            throw new InvalidResumeException("Required DOCX document parts are missing.");
    }
}
