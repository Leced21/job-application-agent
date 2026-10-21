using System.Security.Cryptography;
using System.Text;

namespace JobApplicationAgent.Documents.Domain;

public sealed class DocumentComposition
{
    private DocumentComposition() { }
    public Guid DraftId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Kind { get; private set; } = "";
    public string TemplateVersion { get; private set; } = "";
    public string SourceSnapshotSha256 { get; private set; } = "";
    public string ContentSha256 { get; private set; } = "";
    public string ContentJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public string Decision { get; private set; } = "Pending";
    public Guid? ReviewedByOwnerId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNote { get; private set; }
    public static DocumentComposition Create(Guid ownerId, Guid draftId, string kind, string templateVersion,
        string sourceSha256, string contentJson, DateTime now)
    {
        if (ownerId == Guid.Empty || draftId == Guid.Empty || kind is not ("Resume" or "CoverLetter") ||
            string.IsNullOrWhiteSpace(templateVersion) || templateVersion.Length > 100 || sourceSha256.Length != 64 ||
            string.IsNullOrWhiteSpace(contentJson) || Encoding.UTF8.GetByteCount(contentJson) > 2 * 1024 * 1024 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A composition requires bounded immutable content, source identity and UTC metadata.");
        return new() { OwnerId = ownerId, DraftId = draftId, Kind = kind, TemplateVersion = templateVersion,
            SourceSnapshotSha256 = sourceSha256, ContentJson = contentJson,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contentJson))),
            CreatedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
    public void Review(Guid ownerId, string expectedHash, string decision, string? note, DateTime now)
    {
        if (ownerId != OwnerId || decision is not ("Approved" or "Rejected") || note?.Length > 1000 ||
            now.Kind != DateTimeKind.Utc || now < CreatedAtUtc) throw new ArgumentException("Invalid composition review.");
        if (expectedHash != ContentSha256) throw new DraftConflictException("Review the exact content hash returned by the composition endpoint.");
        if (Decision == decision && ReviewNote == note) return;
        if (Decision != "Pending") throw new DraftConflictException("The content review is final; prepare a new draft to change its content or decision.");
        Decision = decision; ReviewNote = note; ReviewedByOwnerId = ownerId;
        ReviewedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }
}
