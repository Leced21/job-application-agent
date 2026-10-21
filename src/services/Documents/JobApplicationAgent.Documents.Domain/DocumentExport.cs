using System.Security.Cryptography;

namespace JobApplicationAgent.Documents.Domain;

public sealed class DocumentExport
{
    private DocumentExport() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid DraftId { get; private set; }
    public string Format { get; private set; } = "";
    public string RendererVersion { get; private set; } = "";
    public string CompositionSha256 { get; private set; } = "";
    public string FileSha256 { get; private set; } = "";
    public string FileName { get; private set; } = "";
    public string MediaType { get; private set; } = "";
    public byte[] Content { get; private set; } = [];
    public int Length { get; private set; }
    public int? PageCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ApprovedAtUtc { get; private set; }
    public static DocumentExport Create(Guid ownerId, Guid draftId, string format, string rendererVersion, string compositionSha,
        byte[] content, int? pages, DateTime approvedAt, DateTime now)
    {
        if (ownerId == Guid.Empty || draftId == Guid.Empty || format is not ("Pdf" or "Docx") || compositionSha.Length != 64 ||
            string.IsNullOrWhiteSpace(rendererVersion) || rendererVersion.Length > 100 || content.Length is < 1 or > 5242880 ||
            now.Kind != DateTimeKind.Utc || approvedAt.Kind != DateTimeKind.Utc || approvedAt > now ||
            (format == "Pdf" ? pages is not (>= 1 and <= 40) : pages is not null))
            throw new ArgumentException("An export requires approved content, bounded bytes, valid format and UTC metadata.");
        return new() { Id = Guid.NewGuid(), OwnerId = ownerId, DraftId = draftId, Format = format, RendererVersion = rendererVersion,
            CompositionSha256 = compositionSha, Content = content.ToArray(), Length = content.Length, PageCount = pages,
            FileSha256 = Convert.ToHexStringLower(SHA256.HashData(content)),
            FileName = $"document-{draftId:N}.{(format == "Pdf" ? "pdf" : "docx")}",
            MediaType = format == "Pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ApprovedAtUtc = approvedAt, CreatedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
