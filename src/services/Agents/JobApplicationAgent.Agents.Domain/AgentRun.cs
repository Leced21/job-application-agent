using System.Security.Cryptography;
using System.Text;

namespace JobApplicationAgent.Agents.Domain;

public enum AgentKind { ProfileAnalysis = 1, JobAnalysis = 2, MatchingEnrichment = 3, ResumeAdaptation = 4, CoverLetter = 5, ApplicationPreparation = 6, FollowUpSuggestion = 7 }
public enum AgentRunState { Running = 1, PendingReview = 2, Approved = 3, Rejected = 4, Failed = 5 }
public enum AgentFailure { UnsupportedAgent = 1, SourceRejected = 2, InputLimit = 3, BudgetRejected = 4, ModelUnavailable = 5, TimedOut = 6, Cancelled = 7, InvalidOutput = 8, UsageViolation = 9, SourceUnavailable = 10, BudgetUnavailable = 11, Interrupted = 12 }
public sealed record AgentCommand(Guid RequestId, AgentKind Kind, Guid ResourceId);
public sealed record FrozenAgentSource(Guid OwnerId, Guid ResourceId, string Text, string Sha256);
public sealed record AgentAccounting(long ChargedTokens, decimal ChargedUsd, bool UsageKnown);
public sealed record AgentExecutionTrace(string ModelId, string? PromptVersion, string? SourceSha256 = null, string? PromptSha256 = null,
    string? PreparationSha256 = null, string? TokenizerSha256 = null);
public sealed record AgentProposal(FrozenAgentSource Source, string ModelId, string PromptVersion, string PromptSha256, string OutputJson, string OutputSha256);
public sealed record AgentReview(long ExpectedRevision, string OutputSha256, bool Confirmed, bool Approved, string? Note = null);

public sealed record AgentRun
{
    private AgentRun() { }
    public Guid Id { get; private init; }
    public Guid OwnerId { get; private init; }
    public AgentCommand Command { get; private init; } = null!;
    public string RequestSha256 { get; private init; } = "";
    public AgentRunState State { get; private init; }
    public long Revision { get; private init; }
    public AgentProposal? Proposal { get; private init; }
    public AgentFailure? Failure { get; private init; }
    public AgentExecutionTrace? Trace { get; private init; }
    public AgentAccounting Accounting { get; private init; } = new(0, 0, true);
    public string? ReviewNote { get; private init; }
    public DateTime CreatedAtUtc { get; private init; }
    public DateTime UpdatedAtUtc { get; private init; }
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // Replay persisted snapshots through the same invariants as live transitions.
    public static AgentRun Restore(AgentRunSnapshot snapshot)
    {
        if (snapshot.Id == Guid.Empty || (snapshot.State == AgentRunState.Running && snapshot.CompletedAtUtc is not null))
            throw new ArgumentException("Invalid run identity or completion date.");
        var run = Start(snapshot.OwnerId, snapshot.Command, snapshot.CreatedAtUtc) with { Id = snapshot.Id };
        if (snapshot.State != AgentRunState.Running)
        {
            run = run.Finish(snapshot.Proposal, snapshot.Failure, snapshot.Accounting,
                snapshot.CompletedAtUtc ?? throw new ArgumentException("Completion date required."),
                snapshot.Trace ?? throw new ArgumentException("Trace required."));
            if (snapshot.State is AgentRunState.Approved or AgentRunState.Rejected)
                run = run.Review(snapshot.OwnerId, new(run.Revision, run.Proposal!.OutputSha256, true,
                    snapshot.State == AgentRunState.Approved, snapshot.ReviewNote), snapshot.UpdatedAtUtc);
        }
        if (run.State != snapshot.State || run.Revision != snapshot.Revision || run.UpdatedAtUtc != snapshot.UpdatedAtUtc ||
            run.RequestSha256 != snapshot.RequestSha256 || run.Proposal != snapshot.Proposal || run.Failure != snapshot.Failure ||
            run.Accounting != snapshot.Accounting || run.Trace != snapshot.Trace || run.ReviewNote != snapshot.ReviewNote)
            throw new ArgumentException("Invalid persisted run.");
        return run;
    }
    public static AgentRun Start(Guid owner, AgentCommand command, DateTime now)
    {
        if (owner == Guid.Empty || command.RequestId == Guid.Empty || command.ResourceId == Guid.Empty || !Enum.IsDefined(command.Kind) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Valid owner, request, resource, agent and UTC date required.");
        return new() { Id = Guid.NewGuid(), OwnerId = owner, Command = command, State = AgentRunState.Running, Revision = 1,
            RequestSha256 = Hash($"{(int)command.Kind}:{command.ResourceId:D}"), CreatedAtUtc = now, UpdatedAtUtc = now };
    }
    public AgentRun Finish(AgentProposal? proposal, AgentFailure? failure, AgentAccounting accounting, DateTime now, AgentExecutionTrace trace)
    {
        if (State != AgentRunState.Running || now.Kind != DateTimeKind.Utc || now < UpdatedAtUtc || accounting.ChargedTokens < 0 || accounting.ChargedUsd < 0 ||
            (proposal is null) == (failure is null) || (failure is { } code && !Enum.IsDefined(code))) throw new InvalidOperationException("Invalid run completion.");
        if (proposal is not null && (proposal.Source.OwnerId != OwnerId || proposal.Source.ResourceId != Command.ResourceId ||
            Hash(proposal.Source.Text) != proposal.Source.Sha256 || Hash(proposal.OutputJson) != proposal.OutputSha256))
            throw new ArgumentException("Proposal identity or hash mismatch.");
        return this with { State = proposal is null ? AgentRunState.Failed : AgentRunState.PendingReview, Proposal = proposal,
            Failure = failure, Accounting = accounting, Trace = trace, Revision = Revision + 1, UpdatedAtUtc = now };
    }
    public AgentRun Review(Guid owner, AgentReview review, DateTime now)
    {
        if (owner != OwnerId) throw new UnauthorizedAccessException("Owner required.");
        if (State != AgentRunState.PendingReview || review.ExpectedRevision != Revision || !review.Confirmed || review.OutputSha256 != Proposal!.OutputSha256)
            throw new InvalidOperationException("Confirm the exact pending proposal and revision.");
        if (now.Kind != DateTimeKind.Utc || now < UpdatedAtUtc || review.Note?.Length > 1000 || review.Note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Bounded review note and UTC date required.");
        return this with { State = review.Approved ? AgentRunState.Approved : AgentRunState.Rejected, ReviewNote = review.Note?.Trim(), Revision = Revision + 1, UpdatedAtUtc = now };
    }
}

public sealed record AgentRunSnapshot(Guid Id, Guid OwnerId, AgentCommand Command, string RequestSha256,
    AgentRunState State, long Revision, AgentProposal? Proposal, AgentFailure? Failure, AgentExecutionTrace? Trace,
    AgentAccounting Accounting, string? ReviewNote, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? CompletedAtUtc)
{
    public static AgentRunSnapshot From(AgentRun run, DateTime? completedAt = null) => new(run.Id, run.OwnerId,
        run.Command, run.RequestSha256, run.State, run.Revision, run.Proposal, run.Failure, run.Trace, run.Accounting,
        run.ReviewNote, run.CreatedAtUtc, run.UpdatedAtUtc, run.State == AgentRunState.Running ? null : completedAt ?? run.UpdatedAtUtc);
}

public sealed record AgentAuditEntry(Guid RunId, Guid OwnerId, AgentCommand Command, string RequestSha256, AgentRunState State,
    long Revision, AgentFailure? Failure, AgentExecutionTrace? Trace, string? OutputSha256, AgentAccounting Accounting, DateTime AtUtc)
{
    public static AgentAuditEntry From(AgentRun run) => new(run.Id, run.OwnerId, run.Command, run.RequestSha256, run.State,
        run.Revision, run.Failure, run.Trace, run.Proposal?.OutputSha256, run.Accounting, run.UpdatedAtUtc);
}
