using System.Security.Cryptography;

namespace JobApplicationAgent.Documents.Domain;

public sealed class ResumeDocument
{
    private ResumeDocument() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string FileName { get; private set; } = "";
    public string MediaType { get; private set; } = "";
    public int Length { get; private set; }
    public string Sha256 { get; private set; } = "";
    public DateTime UploadedAtUtc { get; private set; }
    public string Status { get; private set; } = "UploadedUnanalysed";
    public byte[] Content { get; private set; } = [];
    public static ResumeDocument Create(Guid ownerId, string fileName, string mediaType, byte[] bytes, DateTime now)
    {
        if (ownerId == Guid.Empty || now.Kind != DateTimeKind.Utc || bytes.Length is < 1 or > 5242880)
            throw new ArgumentException("A document requires an owner, a UTC date and bounded content.");
        return new() { Id = Guid.NewGuid(), OwnerId = ownerId, FileName = fileName, MediaType = mediaType,
            Length = bytes.Length, Content = bytes.ToArray(), Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            UploadedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
