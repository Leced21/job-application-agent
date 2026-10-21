using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.Application;

public interface IApplicationDocumentClient
{
    Task<ApplicationDocument> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, Guid exportId, CancellationToken ct);
}
public sealed class CandidacyReviewService(ICandidacyTrackingRepository repository, IApplicationDocumentClient documents, TimeProvider clock)
{
    public async Task<Candidacy?> AttachAsync(Guid owner, Guid id, long revision, Guid exportId, CancellationToken ct)
    {
        if (exportId == Guid.Empty) throw new ArgumentException("Choose an export.");
        var c = await LoadAsync(owner, id, revision, CandidacyStatus.Draft, ct);
        if (c is null) return null;
        var verified = await documents.VerifyAsync(owner, c.ProfileId, c.JobId, exportId, ct);
        if (verified.ExportId != exportId) throw new ApplicationSourceRejectedException("Document identity mismatch.");
        return await repository.MutateAsync(owner, id, current => current.AttachVerifiedDocument(verified, owner, revision, Now(current)), ct);
    }
    public Task<Candidacy?> RequestReviewAsync(Guid owner, Guid id, long revision, CancellationToken ct) =>
        TransitionAsync(owner, id, revision, false, ct);
    public Task<Candidacy?> ApproveAsync(Guid owner, Guid id, long revision, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) throw new InvalidOperationException("Explicit confirmation required.");
        return TransitionAsync(owner, id, revision, true, ct);
    }
    public Task<Candidacy?> ReturnToDraftAsync(Guid owner, Guid id, long revision, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.ReturnToDraft(owner, revision, Now(c)), ct);
    private async Task<Candidacy?> TransitionAsync(Guid owner, Guid id, long revision, bool approve, CancellationToken ct)
    {
        var c = await LoadAsync(owner, id, revision, approve ? CandidacyStatus.ReadyForReview : CandidacyStatus.Draft, ct);
        if (c is null) return null;
        if (c.Resume is null || c.CoverLetter is null) throw new InvalidOperationException("Both verified documents are required.");
        foreach (var document in new[] { c.Resume, c.CoverLetter })
        {
            var verified = await documents.VerifyAsync(owner, c.ProfileId, c.JobId, document.ExportId, ct);
            if (verified != document) throw new ApplicationSourceRejectedException("Document no longer matches the selected export.");
        }
        return await repository.MutateAsync(owner, id, current => {
            if (approve) current.Approve(owner, revision, true, Now(current));
            else current.RequestReview(owner, revision, Now(current));
        }, ct);
    }
    private async Task<Candidacy?> LoadAsync(Guid owner, Guid id, long revision, CandidacyStatus expectedStatus, CancellationToken ct)
    {
        var c = await repository.GetAsync(owner, id, ct);
        if (c is not null && (c.Revision != revision || c.Status != expectedStatus)) throw new InvalidOperationException("Stale revision or invalid state.");
        return c;
    }
    private DateTime Now(Candidacy c)
    { var now = clock.GetUtcNow().UtcDateTime; return now < c.Events[^1].AtUtc ? c.Events[^1].AtUtc : now; }
}
