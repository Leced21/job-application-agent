using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record AgentQuota(long MaxTokens, decimal MaxUsd)
{
    public void Validate() { if (MaxTokens < 0 || MaxUsd < 0) throw new ArgumentException("Nonnegative server quota required."); }
}
public sealed record AgentBudget(long MaxTokens, decimal MaxUsd, long ChargedTokens, decimal ChargedUsd, long HeldTokens, decimal HeldUsd);
public sealed record AgentRunSummary(Guid Id, Guid RequestId, AgentKind Kind, AgentRunState State, long Revision, DateTime CreatedAtUtc);

// Every mutation takes the same transaction-scoped owner lock. Multiple API instances therefore share
// one quota boundary, without relying on process locks or resetting accounting when the server restarts.
public sealed class PostgresAgentRuns(IDbContextFactory<AgentsDbContext> factory, AgentQuota quota, TimeProvider clock) : IAgentRunRepository
{
    private static AgentRunSnapshot Snapshot(AgentRunRow row) => JsonSerializer.Deserialize<AgentRunSnapshot>(row.Snapshot)
        ?? throw new InvalidOperationException("Missing run snapshot.");
    private static AgentRun Read(AgentRunRow row)
    {
        var run = AgentRun.Restore(Snapshot(row));
        if (run.Id != row.Id || run.OwnerId != row.OwnerId || run.Command.RequestId != row.RequestId || (int)run.State != row.State ||
            run.Accounting.ChargedTokens != row.ChargedTokens || run.Accounting.ChargedUsd != row.ChargedUsd)
            throw new InvalidOperationException("Run metadata mismatch.");
        return run;
    }
    private static void Write(AgentsDbContext db, AgentRunRow row, AgentRun run, DateTime? completedAt = null)
    {
        row.State = (int)run.State;
        row.Snapshot = JsonSerializer.Serialize(AgentRunSnapshot.From(run, completedAt));
        row.ChargedTokens = run.Accounting.ChargedTokens; row.ChargedUsd = run.Accounting.ChargedUsd;
        db.Audits.Add(new() { RunId = run.Id, Revision = run.Revision, Metadata = JsonSerializer.Serialize(AgentAuditEntry.From(run)) });
    }
    private async Task<T> Mutate<T>(Guid owner, Func<AgentsDbContext, Task<T>> action, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = owner.ToString("D");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        var result = await action(db);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return result;
    }
    public Task<AgentRun> BeginAsync(AgentRun candidate, CancellationToken ct) => Mutate(candidate.OwnerId, async db => {
        if (candidate.State != AgentRunState.Running || candidate.Revision != 1) throw new ArgumentException("New run required.");
        var old = await db.Runs.SingleOrDefaultAsync(x => x.OwnerId == candidate.OwnerId && x.RequestId == candidate.Command.RequestId, ct);
        if (old is not null)
        {
            var stored = Read(old);
            if (stored.RequestSha256 != candidate.RequestSha256) throw new InvalidOperationException("Request identifier already used for another command.");
            return stored;
        }
        var row = new AgentRunRow { Id = candidate.Id, OwnerId = candidate.OwnerId, RequestId = candidate.Command.RequestId,
            ExpiresAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(5) };
        db.Runs.Add(row); Write(db, row, candidate); return candidate;
    }, ct);
    private static async Task<AgentBudget> Budget(AgentsDbContext db, Guid owner, AgentQuota quota, CancellationToken ct)
    {
        var values = await db.Runs.Where(x => x.OwnerId == owner).GroupBy(x => x.OwnerId)
            .Select(g => new { Tokens = g.Sum(x => x.ChargedTokens), Usd = g.Sum(x => x.ChargedUsd),
                HeldTokens = g.Sum(x => x.ReservedTokens ?? 0), HeldUsd = g.Sum(x => x.ReservedUsd ?? 0) }).SingleOrDefaultAsync(ct);
        return new(quota.MaxTokens, quota.MaxUsd, values?.Tokens ?? 0, values?.Usd ?? 0, values?.HeldTokens ?? 0, values?.HeldUsd ?? 0);
    }
    public async Task<AgentBudget> GetBudgetAsync(Guid owner, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct); return await Budget(db, owner, quota, ct);
    }
    public Task<bool> ReserveAsync(Guid owner, Guid runId, AgentReservation reservation, CancellationToken ct) => Mutate(owner, async db => {
        quota.Validate();
        if (reservation.MaxTokens < 0 || reservation.MaxUsd < 0) throw new ArgumentException("Invalid reservation.");
        var row = await db.Runs.SingleOrDefaultAsync(x => x.Id == runId && x.OwnerId == owner, ct);
        if (row is null || Read(row).State != AgentRunState.Running || row.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime) return false;
        if (row.ReservedTokens.HasValue) return row.ReservedTokens == reservation.MaxTokens && row.ReservedUsd == reservation.MaxUsd;
        var budget = await Budget(db, owner, quota, ct);
        if (reservation.MaxTokens > budget.MaxTokens - budget.ChargedTokens - budget.HeldTokens ||
            reservation.MaxUsd > budget.MaxUsd - budget.ChargedUsd - budget.HeldUsd) return false;
        row.ReservedTokens = reservation.MaxTokens; row.ReservedUsd = reservation.MaxUsd; return true;
    }, ct);
    public async Task CompleteAsync(AgentRun completed, CancellationToken ct) => await Mutate(completed.OwnerId, async db => {
        var row = await db.Runs.SingleAsync(x => x.Id == completed.Id && x.OwnerId == completed.OwnerId, ct);
        var old = Read(row);
        if (old == completed) return true; // An acknowledgement may have been lost after commit.
        if (old.State != AgentRunState.Running || completed.Revision != 2 || completed.State is not (AgentRunState.PendingReview or AgentRunState.Failed))
            throw new InvalidOperationException("Run already completed.");
        var expected = old.Finish(completed.Proposal, completed.Failure, completed.Accounting, completed.UpdatedAtUtc, completed.Trace!);
        if (expected != completed) throw new ArgumentException("Completion identity mismatch.");
        if (!row.ReservedTokens.HasValue && completed.Accounting != new AgentAccounting(0, 0, true)) throw new ArgumentException("No reservation.");
        if (!completed.Accounting.UsageKnown && (completed.Accounting.ChargedTokens != row.ReservedTokens || completed.Accounting.ChargedUsd != row.ReservedUsd))
            throw new ArgumentException("Unknown usage must charge the full reservation.");
        row.ReservedTokens = null; row.ReservedUsd = null; Write(db, row, completed); return true;
    }, ct);
    public async Task<AgentRun?> GetAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        return row is null ? null : Read(row);
    }
    public async Task<AgentRun?> GetLatestApprovedAsync(Guid owner, AgentKind kind, Guid resourceId, CancellationToken ct)
    {
        if (owner == Guid.Empty || resourceId == Guid.Empty || !Enum.IsDefined(kind)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        // The UUID narrows the snapshot search; the deserialized command is checked again before returning anything.
        var marker = $"%{resourceId:D}%";
        var rows = await db.Runs.AsNoTracking().Where(x => x.OwnerId == owner && x.State == (int)AgentRunState.Approved &&
            EF.Functions.Like(x.Snapshot, marker)).OrderByDescending(x => x.ExpiresAtUtc).ToListAsync(ct);
        return rows.Select(Read).FirstOrDefault(run => run.State == AgentRunState.Approved && run.Command.Kind == kind &&
            run.Command.ResourceId == resourceId && run.Proposal is not null);
    }
    public async Task<AgentRun?> GetByRequestAsync(Guid owner, Guid request, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.RequestId == request, ct);
        return row is null ? null : Read(row);
    }
    public async Task<IReadOnlyList<AgentRunSummary>> ListAsync(Guid owner, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 50) throw new ArgumentException("Bounded pagination required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Runs.AsNoTracking().Where(x => x.OwnerId == owner).OrderByDescending(x => x.ExpiresAtUtc)
            .ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return rows.Select(Read).Select(x => new AgentRunSummary(x.Id, x.Command.RequestId, x.Command.Kind, x.State, x.Revision, x.CreatedAtUtc)).ToArray();
    }
    public Task<bool> TryReviewAsync(AgentRun reviewed, long expectedRevision, CancellationToken ct) => Mutate(reviewed.OwnerId, async db => {
        var row = await db.Runs.SingleOrDefaultAsync(x => x.Id == reviewed.Id && x.OwnerId == reviewed.OwnerId, ct);
        if (row is null) return false;
        var old = Read(row);
        if (old.State != AgentRunState.PendingReview || old.Revision != expectedRevision) return false;
        var expected = old.Review(reviewed.OwnerId, new(expectedRevision, reviewed.Proposal!.OutputSha256, true,
            reviewed.State == AgentRunState.Approved, reviewed.ReviewNote), reviewed.UpdatedAtUtc);
        if (expected != reviewed) throw new ArgumentException("Review mismatch.");
        Write(db, row, reviewed, Snapshot(row).CompletedAtUtc); return true;
    }, ct);
    public Task<AgentRun?> RecoverAsync(Guid owner, Guid id, CancellationToken ct) => Mutate(owner, async db => {
        var row = await db.Runs.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner, ct);
        if (row is null) return null;
        var old = Read(row);
        if (old.State != AgentRunState.Running) return old;
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < row.ExpiresAtUtc) throw new InvalidOperationException("Execution lease has not expired.");
        var accounting = row.ReservedTokens.HasValue ? new AgentAccounting(row.ReservedTokens.Value, row.ReservedUsd!.Value, false) : new(0, 0, true);
        var completed = old.Finish(null, AgentFailure.Interrupted, accounting, now < old.UpdatedAtUtc ? old.UpdatedAtUtc : now,
            new("unconfirmed", null));
        row.ReservedTokens = null; row.ReservedUsd = null; Write(db, row, completed); return completed;
    }, ct);
    public async Task<IReadOnlyList<AgentAuditEntry>> GetAuditAsync(Guid owner, Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Audits.Where(a => a.RunId == id && db.Runs.Any(r => r.Id == a.RunId && r.OwnerId == owner))
            .OrderBy(a => a.Revision).Select(a => a.Metadata).ToListAsync(ct);
        return rows.Select(x => JsonSerializer.Deserialize<AgentAuditEntry>(x)!).ToArray();
    }
}
