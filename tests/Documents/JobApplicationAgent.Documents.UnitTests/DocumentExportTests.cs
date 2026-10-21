using System.Security.Cryptography;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class DocumentExportTests
{
    [Fact]
    public void Export_ShouldCopyBytesAndBindFileHashToApprovedContent()
    {
        var bytes = new byte[] { 1, 2, 3 }; var now = DateTime.UtcNow;
        var export = DocumentExport.Create(Guid.NewGuid(), Guid.NewGuid(), "Pdf", "v1", new string('a', 64), bytes, 1, now.AddMinutes(-1), now);
        bytes[0] = 9; Assert.Equal(1, export.Content[0]); Assert.Equal(3, export.Length);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(export.Content)), export.FileSha256); Assert.EndsWith(".pdf", export.FileName);
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("draft")]
    [InlineData("format")]
    [InlineData("hash")]
    [InlineData("renderer")]
    [InlineData("empty")]
    [InlineData("size")]
    [InlineData("pages")]
    [InlineData("date")]
    [InlineData("approval")]
    public void InvalidExport_ShouldBeRejected(string invalid)
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => DocumentExport.Create(invalid == "owner" ? Guid.Empty : Guid.NewGuid(),
            invalid == "draft" ? Guid.Empty : Guid.NewGuid(), invalid == "format" ? "Unknown" : "Pdf", invalid == "renderer" ? "" : "v1",
            invalid == "hash" ? "bad" : new string('a', 64), invalid == "empty" ? [] : invalid == "size" ? new byte[5242881] : [1],
            invalid == "pages" ? 41 : 1, invalid == "approval" ? now.AddHours(1) : now.AddHours(-1),
            invalid == "date" ? DateTime.SpecifyKind(now, DateTimeKind.Unspecified) : now));
    }
}
