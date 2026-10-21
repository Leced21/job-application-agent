namespace JobApplicationAgent.Documents.Domain;

public sealed class DocumentApplicationBinding
{
    private DocumentApplicationBinding() { }
    public Guid ExportId { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ProfileId { get; private set; }
    public Guid JobId { get; private set; }
    public Guid DraftId { get; private set; }
    public string Kind { get; private set; } = "";
    public string FileSha256 { get; private set; } = "";
    public string CompositionSha256 { get; private set; } = "";
    public string SnapshotSha256 { get; private set; } = "";
    public DateTime ConfirmedAtUtc { get; private set; }
    public static DocumentApplicationBinding Create(DocumentExport export, PreparedDocumentDraft draft, Guid profileId, DateTime atUtc)
    {
        if (profileId == Guid.Empty || export.OwnerId != draft.OwnerId || export.DraftId != draft.Id || atUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid document/profile binding.");
        return new() { ExportId = export.Id, OwnerId = export.OwnerId, ProfileId = profileId, JobId = draft.JobId,
            DraftId = draft.Id, Kind = draft.Kind, FileSha256 = export.FileSha256, CompositionSha256 = export.CompositionSha256,
            SnapshotSha256 = draft.SnapshotSha256, ConfirmedAtUtc = new(atUtc.Ticks - atUtc.Ticks % 10, DateTimeKind.Utc) };
    }
}
