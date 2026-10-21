using System.IO.Compression;
using System.Text;

namespace JobApplicationAgent.Documents.Tests;

public static class ResumeSamples
{
    // Deliberately tests only the upload envelope; this is not a parsed/verified CV.
    public static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.7\n% synthetic upload envelope\n%%EOF\n");
    public static byte[] Docx(string? extraPath = null, string? extraContent = null)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            Write(archive, "[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" /></Types>
                """);
            Write(archive, "_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" /></Relationships>
                """);
            Write(archive, "word/document.xml", """
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>Synthetic CV</w:t></w:r></w:p></w:body></w:document>
                """);
            if (extraPath is not null) Write(archive, extraPath, extraContent ?? "test");
        }
        return memory.ToArray();
    }
    private static void Write(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
