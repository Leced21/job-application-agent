using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.Application;

public sealed record CandidacySummary(Guid Id, Guid ProfileId, Guid JobId, string JobTitle, string CompanyName,
    CandidacyStatus Status, long Revision, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CandidacyPage(int Page, int PageSize, IReadOnlyList<CandidacySummary> Items);
public interface ICandidacyTrackingRepository
{
    Task<Candidacy?> GetAsync(Guid ownerId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<CandidacySummary>> ListAsync(Guid ownerId, int page, int pageSize, CandidacyStatus? status, CancellationToken ct);
    Task<Candidacy?> MutateAsync(Guid ownerId, Guid id, Action<Candidacy> mutation, CancellationToken ct);
}
public sealed class ApplicationSourceUnavailableException : Exception;
public sealed class ApplicationSourceRejectedException(string message) : Exception(message);

/// <summary>Server-owned allowlist of submission platform capabilities. Implementations must never accept client supplied values.</summary>
public interface ISubmissionPlatformCatalog
{
    bool AutomationEnabled { get; }
    IReadOnlyList<string> EligiblePlatformIds { get; }
    bool TryGet(string platformId, out SubmissionPlatformCapability platform);
}

public sealed class SubmissionPlatformCatalog : ISubmissionPlatformCatalog
{
    private readonly IReadOnlyDictionary<string, SubmissionPlatformCapability> platforms;

    public SubmissionPlatformCatalog(bool automationEnabled, IEnumerable<SubmissionPlatformCapability> platforms)
    {
        AutomationEnabled = automationEnabled;
        var entries = new Dictionary<string, SubmissionPlatformCapability>(StringComparer.Ordinal);
        foreach (var platform in platforms)
        {
            if (platform is null || platform.PlatformId is not { } platformId ||
                !System.Text.RegularExpressions.Regex.IsMatch(platformId, "^[a-z0-9][a-z0-9.-]{0,63}$"))
                throw new ArgumentException("Platform IDs must be lowercase identifiers containing only letters, digits, dots and dashes.", nameof(platforms));
            if (!entries.TryAdd(platformId, platform))
                throw new ArgumentException($"Duplicate submission platform ID '{platformId}'.", nameof(platforms));
        }
        this.platforms = entries;
    }

    public bool AutomationEnabled { get; }
    public IReadOnlyList<string> EligiblePlatformIds => platforms.Values
        .Where(platform => platform.HasOfficialApi && platform.TermsAllowAutomation &&
            !platform.RequiresCaptcha && platform.SupportsIdempotencyKey)
        .Select(platform => platform.PlatformId)
        .Order(StringComparer.Ordinal)
        .ToArray();
    public bool TryGet(string platformId, out SubmissionPlatformCapability platform)
    {
        if (platformId is not null && platforms.TryGetValue(platformId, out var registered))
        {
            platform = registered;
            return true;
        }
        platform = null!;
        return false;
    }
}

public sealed class CandidacyTrackingService(ICandidacyTrackingRepository repository, TimeProvider clock, ISubmissionPlatformCatalog submissionPlatforms)
{
    public Task<Candidacy?> GetAsync(Guid owner, Guid id, CancellationToken ct) => repository.GetAsync(owner, id, ct);
    public async Task<CandidacyPage> ListAsync(Guid owner, int page, int pageSize, CandidacyStatus? status, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100 || status is not null && !Enum.IsDefined(status.Value))
            throw new ArgumentException("Invalid pagination or status.");
        return new(page, pageSize, await repository.ListAsync(owner, page, pageSize, status, ct));
    }
    public Task<Candidacy?> NoteAsync(Guid owner, Guid id, long revision, string note, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.AddNote(owner, revision, note, Now(c)), ct);
    public Task<Candidacy?> WithdrawAsync(Guid owner, Guid id, long revision, bool confirmed, string reason, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.Withdraw(owner, revision, confirmed, reason, Now(c)), ct);
    public Task<Candidacy?> DeclareSubmissionAsync(Guid owner, Guid id, long revision, bool confirmed, ManualSubmission submission, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.DeclareManualSubmission(owner, revision, confirmed, submission, Now(c)), ct);
    public Task<Candidacy?> ApproveControlledSubmissionAsync(Guid owner, Guid id, long revision, bool confirmed, string reviewSha256, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.ApproveControlledSubmission(owner, revision, confirmed, reviewSha256, Now(c)), ct);
    public Task<Candidacy?> PrepareControlledSubmissionAttemptAsync(Guid owner, Guid id, long revision, bool confirmed, Guid attemptId,
        string platformId, string reviewSha256, CancellationToken ct)
    {
        if (!submissionPlatforms.TryGet(platformId, out var platform))
            throw new InvalidOperationException("The requested submission platform is not registered by the server.");
        return repository.MutateAsync(owner, id, c => c.PrepareControlledSubmissionAttempt(owner, revision,
            submissionPlatforms.AutomationEnabled, confirmed, attemptId, platform, reviewSha256, Now(c)), ct);
    }
    public Task<Candidacy?> RecordControlledSubmissionResultAsync(Guid owner, Guid id, long revision, bool confirmed,
        ControlledSubmissionResult result, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.RecordControlledSubmissionResult(owner, revision, confirmed, result, Now(c)), ct);
    public Task<Candidacy?> RecruiterUpdateAsync(Guid owner, Guid id, long revision, bool confirmed, CandidacyStatus target, RecruiterUpdate update, CancellationToken ct) =>
        repository.MutateAsync(owner, id, c => c.RecordRecruiterUpdate(owner, revision, confirmed, target, update, Now(c)), ct);
    private DateTime Now(Candidacy c)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return now < c.Events[^1].AtUtc ? c.Events[^1].AtUtc : now;
    }
}
