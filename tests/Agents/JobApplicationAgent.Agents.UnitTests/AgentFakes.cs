using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

internal sealed class AgentFixture
{
    public Guid Owner { get; } = Guid.NewGuid();
    public AgentCommand Command { get; } = new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid());
    public MemoryRuns Runs { get; } = new();
    public FakeSource Source { get; }
    public FakeModel Model { get; } = new();
    // Synthetic test tariffs only; these are not any provider's prices.
    public AgentPolicy Policy { get; set; } = new("simulated-model", 1000, 500, 1m, 2m, 4m, TimeSpan.FromSeconds(2));
    public AgentFixture() { Source = new(new(Owner, Command.ResourceId, "SQL required. Rust optional.", AgentRun.Hash("SQL required. Rust optional."))); }
    public AgentOrchestrator Engine => new(Runs, Source, Model, Policy, TimeProvider.System);
    public static string Output(string value = "SQL", int start = 0, string category = "Skill") => JsonSerializer.Serialize(new {
        schemaVersion = "job-analysis-v1", requirements = new[] { new { category, value, start, length = value.Length } } });
    public sealed class FakeSource(FrozenAgentSource source) : IAgentSourceReader
    {
        public FrozenAgentSource? Value { get; set; } = source;
        public int Calls { get; private set; }
        public Exception? Error { get; set; }
        public Task<FrozenAgentSource?> ReadAsync(Guid owner, AgentCommand command, CancellationToken ct)
        { Calls++; ct.ThrowIfCancellationRequested(); if (Error is not null) throw Error; return Task.FromResult(Value); }
    }
    public sealed class FakeModel : IAgentModel
    {
        public int Calls { get; private set; }
        public int Count { get; set; } = 100;
        public Func<ModelRequest, CancellationToken, Task<ModelResponse>> Respond { get; set; } = (_, _) => Task.FromResult(new ModelResponse(Output(), 100, 40));
        public ModelRequest? LastRequest { get; private set; }
        public Func<string, ModelPrompt, CancellationToken, Task<PreparedModelInput>>? Prepare { get; set; }
        public Task<PreparedModelInput> PrepareAsync(string model, ModelPrompt prompt, CancellationToken ct) =>
            Prepare?.Invoke(model, prompt, ct) ?? Task.FromResult(new PreparedModelInput(model, prompt, Count));
        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken ct)
        { Calls++; LastRequest = request; return Respond(request, ct); }
    }
}

// Test double only: no production in-memory quota or run store is registered.
internal sealed class MemoryRuns : IAgentRunRepository
{
    private readonly object gate = new();
    private readonly Dictionary<(Guid, Guid), AgentRun> runs = [];
    private readonly Dictionary<Guid, AgentReservation> reserved = [];
    public bool AllowReservation { get; set; } = true;
    public bool FailCompletion { get; set; }
    public bool LoseReservationAck { get; set; }
    public Func<Task>? BeforeReviewCommit { get; set; }
    public long AvailableTokens { get; set; } = 100000;
    public decimal AvailableUsd { get; set; } = 100m;
    public List<AgentAuditEntry> Audit { get; } = [];
    public Task<AgentRun> BeginAsync(AgentRun candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            var key = (candidate.OwnerId, candidate.Command.RequestId);
            if (runs.TryGetValue(key, out var old))
            { if (old.RequestSha256 != candidate.RequestSha256) throw new InvalidOperationException("Request conflict"); return Task.FromResult(old); }
            runs.Add(key, candidate); return Task.FromResult(candidate);
        }
    }
    public Task<bool> ReserveAsync(Guid owner, Guid runId, AgentReservation value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            var run = runs.Values.Single(r => r.Id == runId && r.OwnerId == owner);
            if (run.State != AgentRunState.Running || !AllowReservation || AvailableTokens < value.MaxTokens || AvailableUsd < value.MaxUsd) return Task.FromResult(false);
            reserved.Add(runId, value); AvailableTokens -= value.MaxTokens; AvailableUsd -= value.MaxUsd;
            if (LoseReservationAck) throw new IOException("Synthetic lost acknowledgement");
            return Task.FromResult(true);
        }
    }
    public Task CompleteAsync(AgentRun completed, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lock (gate)
        {
            if (FailCompletion) throw new IOException("Synthetic audit outage");
            var key = (completed.OwnerId, completed.Command.RequestId); var old = runs[key];
            if (old.State != AgentRunState.Running || old.Revision + 1 != completed.Revision) throw new InvalidOperationException("Duplicate completion");
            if (reserved.Remove(completed.Id, out var held)) { AvailableTokens += held.MaxTokens; AvailableUsd += held.MaxUsd; }
            AvailableTokens -= completed.Accounting.ChargedTokens; AvailableUsd -= completed.Accounting.ChargedUsd;
            runs[key] = completed; Audit.Add(AgentAuditEntry.From(completed));
            return Task.CompletedTask;
        }
    }
    public Task<AgentRun?> GetAsync(Guid owner, Guid id, CancellationToken ct)
    { lock (gate) return Task.FromResult(runs.Values.SingleOrDefault(r => r.OwnerId == owner && r.Id == id)); }
    public Task<AgentRun?> GetLatestApprovedAsync(Guid owner, AgentKind kind, Guid resourceId, CancellationToken ct)
    { lock (gate) return Task.FromResult(runs.Values.Where(r => r.OwnerId == owner && r.Command.Kind == kind && r.Command.ResourceId == resourceId && r.State == AgentRunState.Approved).OrderByDescending(r => r.UpdatedAtUtc).FirstOrDefault()); }
    public async Task<bool> TryReviewAsync(AgentRun reviewed, long revision, CancellationToken ct)
    {
        if (BeforeReviewCommit is not null) await BeforeReviewCommit();
        lock (gate)
        {
            var key = (reviewed.OwnerId, reviewed.Command.RequestId); var current = runs[key];
            if (current.Revision != revision || current.State != AgentRunState.PendingReview) return false;
            runs[key] = reviewed; Audit.Add(AgentAuditEntry.From(reviewed)); return true;
        }
    }
}
