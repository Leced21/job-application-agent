using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record PrepareDraftCommand(Guid RequestId, Guid JobId, Guid ResumeId, string Kind, Guid[] FactIds);
public sealed record DraftJobSnapshot(Guid Id, string Title, string CompanyName, string? Location, string? Description,
    string Source, string? SourceUrl, string Status, DateTime UpdatedAtUtc);
public sealed record DraftVersionIdentity(Guid SeriesId, int VersionNumber, string? Label);
public sealed record DraftFactSnapshot(CandidateFactDto Fact, ConfirmProfileDatesCommand? Dates,
    Guid? DatesConfirmedByOwnerId, DateTime? DatesConfirmedAtUtc)
{
    public string DatesOrigin => Dates is null ? "NotProvided" : "OwnerConfirmed";
}
public sealed record DraftResumeSnapshot(ResumeMetadata Resume, DraftVersionIdentity? Version, DraftFactSnapshot[] Facts);
public sealed record DraftSourceSnapshot(string SchemaVersion, DraftJobSnapshot Job, DraftResumeSnapshot Candidate);
public sealed record PreparedDraftSummary(Guid Id, Guid RequestId, Guid ResumeId, Guid JobId, string Kind, string Status, string SnapshotSha256, DateTime CreatedAtUtc)
{
    public static PreparedDraftSummary From(PreparedDocumentDraft draft) => new(draft.Id, draft.RequestId, draft.ResumeId, draft.JobId,
        draft.Kind, draft.Status, draft.SnapshotSha256, draft.CreatedAtUtc);
}
public sealed record PreparedDraftDto(PreparedDraftSummary Draft, DraftSourceSnapshot Snapshot)
{
    public static PreparedDraftDto From(PreparedDocumentDraft draft) => new(PreparedDraftSummary.From(draft), JsonSerializer.Deserialize<DraftSourceSnapshot>(draft.SnapshotJson)!);
}
public sealed record PreparedDraftWriteResult(PreparedDraftDto Document, bool Created);
public sealed class DraftRejectedException(string message) : Exception(message);
public sealed class DraftJobUnavailableException : Exception;
public interface IDraftJobClient { Task<DraftJobSnapshot?> GetAsync(Guid jobId, CancellationToken ct); }
public interface IPreparedDraftRepository
{
    Task<PreparedDocumentDraft?> GetByRequestAsync(Guid ownerId, Guid requestId, CancellationToken ct);
    Task<PreparedDocumentDraft?> GetAsync(Guid ownerId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<PreparedDraftSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct);
    Task<DraftResumeSnapshot?> LoadSourceAsync(Guid ownerId, Guid resumeId, Guid[] factIds, CancellationToken ct);
    Task<(PreparedDocumentDraft Draft, bool Created)> SaveOrGetAsync(PreparedDocumentDraft draft, CancellationToken ct);
}
public sealed class PreparedDraftService(IPreparedDraftRepository repository, IDraftJobClient jobs, TimeProvider clock)
{
    public async Task<PreparedDraftWriteResult?> PrepareAsync(Guid ownerId, PrepareDraftCommand command, CancellationToken ct)
    {
        if (command.RequestId == Guid.Empty || command.JobId == Guid.Empty || command.ResumeId == Guid.Empty ||
            command.Kind is not ("Resume" or "CoverLetter") || command.FactIds is not { Length: >= 1 and <= 50 } ||
            command.FactIds.Any(x => x == Guid.Empty) || command.FactIds.Distinct().Count() != command.FactIds.Length)
            throw new InvalidResumeException("Select a document kind, job, resume, unique request ID and 1–50 distinct fact IDs in the desired order.");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command))));
        var existing = await repository.GetByRequestAsync(ownerId, command.RequestId, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new DraftConflictException("This request ID was used with a different source selection.");
            return new(PreparedDraftDto.From(existing), false);
        }
        var source = await repository.LoadSourceAsync(ownerId, command.ResumeId, command.FactIds, ct);
        if (source is null) return null;
        if (source.Facts.Any(x => x.Fact.Decision != "Approved" || x.Fact.ReviewedByOwnerId != ownerId || x.Fact.ReviewedAtUtc is null))
            throw new DraftRejectedException("Every selected fact must be approved by the source owner.");
        var job = await jobs.GetAsync(command.JobId, ct);
        if (job is null) return null;
        if (job.Status is not ("Discovered" or "Saved")) throw new DraftRejectedException("An ignored or expired job cannot be used for a new draft.");
        var json = JsonSerializer.Serialize(new DraftSourceSnapshot("prepared-draft-v1", job, source));
        if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new DraftRejectedException("The source snapshot exceeds 2 MiB; select fewer facts.");
        var draft = PreparedDocumentDraft.Create(ownerId, command.RequestId, command.ResumeId, command.JobId, command.Kind,
            hash, json, clock.GetUtcNow().UtcDateTime);
        var saved = await repository.SaveOrGetAsync(draft, ct);
        return new(PreparedDraftDto.From(saved.Draft), saved.Created);
    }
    public async Task<PreparedDraftDto?> GetAsync(Guid ownerId, Guid id, CancellationToken ct)
    { var draft = await repository.GetAsync(ownerId, id, ct); return draft is null ? null : PreparedDraftDto.From(draft); }
    public Task<IReadOnlyList<PreparedDraftSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new InvalidResumeException("Invalid draft pagination.");
        return repository.GetPageAsync(ownerId, page, size, ct);
    }
}
