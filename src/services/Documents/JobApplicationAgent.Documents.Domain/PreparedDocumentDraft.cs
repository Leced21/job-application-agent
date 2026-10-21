using System.Security.Cryptography;
using System.Text;

namespace JobApplicationAgent.Documents.Domain;

public sealed class DraftConflictException(string message) : Exception(message);
public sealed class PreparedDocumentDraft
{
    private PreparedDocumentDraft() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid ResumeId { get; private set; }
    public Guid JobId { get; private set; }
    public string Kind { get; private set; } = "";
    public string Status { get; private set; } = "PreparedForReview";
    public string RequestHash { get; private set; } = "";
    public string SnapshotSha256 { get; private set; } = "";
    public string SnapshotJson { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public static PreparedDocumentDraft Create(Guid ownerId, Guid requestId, Guid resumeId, Guid jobId,
        string kind, string requestHash, string snapshotJson, DateTime now)
    {
        if (ownerId == Guid.Empty || requestId == Guid.Empty || resumeId == Guid.Empty || jobId == Guid.Empty ||
            kind is not ("Resume" or "CoverLetter") || requestHash.Length != 64 ||
            string.IsNullOrWhiteSpace(snapshotJson) || Encoding.UTF8.GetByteCount(snapshotJson) > 2 * 1024 * 1024 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A draft requires bounded source snapshots, valid identities and a UTC date.");
        return new() { Id = Guid.NewGuid(), OwnerId = ownerId, RequestId = requestId, ResumeId = resumeId, JobId = jobId,
            Kind = kind, RequestHash = requestHash, SnapshotJson = snapshotJson,
            SnapshotSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))),
            CreatedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
