using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.Application;

public sealed record DueFollowUp(Guid CandidacyId, string JobTitle, string CompanyName, CandidacyStatus Status,
    long Revision, Guid FollowUpId, DateTime DueAtUtc, ContactChannel Channel, string Note);
public sealed record DueFollowUpPage(int Page, int PageSize, DateTime AsOfUtc, IReadOnlyList<DueFollowUp> Items);
public interface ICandidacyFollowUpQuery
{
    Task<IReadOnlyList<DueFollowUp>> DueAsync(Guid owner, DateTime asOfUtc, int page, int size, CancellationToken ct);
}
public sealed class FollowUpService(ICandidacyTrackingRepository repository, ICandidacyFollowUpQuery query, TimeProvider clock)
{
    public Task<Candidacy?> PlanAsync(Guid owner, Guid id, long revision, DateTime due, ContactChannel channel, string note, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.PlanFollowUp(owner, revision, new(Guid.NewGuid(), due, channel, note), Now(c)), ct);
    public Task<Candidacy?> RescheduleAsync(Guid owner, Guid id, Guid followUpId, long revision, DateTime due, ContactChannel channel, string note, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.RescheduleFollowUp(owner, revision, new(followUpId, due, channel, note), Now(c)), ct);
    public Task<Candidacy?> CompleteAsync(Guid owner, Guid id, Guid followUpId, long revision, bool confirmed, DateTime occurred, ContactChannel channel, string outcome, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.CompleteFollowUp(owner, revision, followUpId, confirmed, occurred, channel, outcome, Now(c)), ct);
    public Task<Candidacy?> CancelAsync(Guid owner, Guid id, Guid followUpId, long revision, bool confirmed, string reason, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.CancelFollowUp(owner, revision, followUpId, confirmed, reason, Now(c)), ct);
    public async Task<DueFollowUpPage> DueAsync(Guid owner, int page, int size, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        var now = clock.GetUtcNow().UtcDateTime;
        return new(page, size, now, await query.DueAsync(owner, now, page, size, ct));
    }
    private DateTime Now(Candidacy c)
    { var now = clock.GetUtcNow().UtcDateTime; return now < c.Events[^1].AtUtc ? c.Events[^1].AtUtc : now; }
}
