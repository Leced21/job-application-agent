using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using JobApplicationAgent.Agents.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class PostgresAgentRunsTests(AgentsFactory fixture) : IClassFixture<AgentsFactory>
{
    private IDbContextFactory<AgentsDbContext> Factory => fixture.Services.GetRequiredService<IDbContextFactory<AgentsDbContext>>();
    private PostgresAgentRuns Repo(AgentQuota? quota = null, TimeProvider? clock = null) => new(Factory, quota ?? new(10000, 1), clock ?? TimeProvider.System);
    private static AgentRun New(Guid owner, Guid? request = null) => AgentRun.Start(owner, new(request ?? Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()), DateTime.UtcNow);
    private static AgentRun Fail(AgentRun run, AgentAccounting? accounting = null) => run.Finish(null, AgentFailure.ModelUnavailable,
        accounting ?? new(0, 0, true), DateTime.UtcNow, new("simulated", JobAnalysisPrompt.Version));
    private static AgentRun Proposal(AgentRun run)
    {
        var source = new FrozenAgentSource(run.OwnerId, run.Command.ResourceId, "SQL", AgentRun.Hash("SQL"));
        var json = """{"schemaVersion":"job-analysis-v1","requirements":[]}""";
        return run.Finish(new(source, "simulated", JobAnalysisPrompt.Version, new string('a', 64), json, AgentRun.Hash(json)),
            null, new(140, .00036m, true), DateTime.UtcNow, new("simulated", JobAnalysisPrompt.Version));
    }
    [Fact] public async Task MigrationMatchesModel()
    {
        await using var db = await Factory.CreateDbContextAsync(); Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact] public async Task ConcurrentBeginReturnsOneIdentityAndOneAudit()
    {
        var owner = Guid.NewGuid(); var first = New(owner);
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Repo().BeginAsync(AgentRun.Start(owner, first.Command, DateTime.UtcNow), default)));
        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Single(await Repo().GetAuditAsync(owner, results[0].Id, default));
    }
    [Fact] public async Task ConflictingRequestCannotChangeSource()
    {
        var owner = Guid.NewGuid(); var run = New(owner); await Repo().BeginAsync(run, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo().BeginAsync(New(owner, run.Command.RequestId), default));
    }
    [Fact] public async Task SameRequestIsIndependentAcrossOwners()
    {
        var request = Guid.NewGuid(); var a = await Repo().BeginAsync(New(Guid.NewGuid(), request), default);
        var b = await Repo().BeginAsync(New(Guid.NewGuid(), request), default); Assert.NotEqual(a.Id, b.Id);
        Assert.Null(await Repo().GetAsync(b.OwnerId, a.Id, default)); Assert.Empty(await Repo().GetAuditAsync(b.OwnerId, a.Id, default));
    }
    [Fact] public async Task QuotaReservationIsAtomicAcrossRepositoryInstances()
    {
        var owner = Guid.NewGuid(); var runs = Enumerable.Range(0, 12).Select(_ => New(owner)).ToArray();
        foreach (var run in runs) await Repo().BeginAsync(run, default);
        var results = await Task.WhenAll(runs.Select(r => Repo(new(1500, .004m)).ReserveAsync(owner, r.Id, new(1500, .004m), default)));
        Assert.Single(results, x => x);
        var budget = await Repo().GetBudgetAsync(owner); Assert.Equal(1500, budget.HeldTokens); Assert.Equal(.004m, budget.HeldUsd);
    }
    [Theory] [InlineData(1499, 1)] [InlineData(10000, 0)]
    public async Task EitherQuotaLimitRejectsWithoutHolding(long tokens, decimal usd)
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        Assert.False(await Repo(new(tokens, usd)).ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default));
        Assert.Equal(0, (await Repo().GetBudgetAsync(run.OwnerId)).HeldTokens);
    }
    [Fact] public async Task ReservationAcknowledgementReplayDoesNotDoubleHold()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        Assert.True(await Repo().ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default));
        Assert.True(await Repo().ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default));
        Assert.False(await Repo().ReserveAsync(run.OwnerId, run.Id, new(1501, .004m), default));
        Assert.Equal(1500, (await Repo().GetBudgetAsync(run.OwnerId)).HeldTokens);
    }
    [Fact] public async Task CompletionReplayChargesOnceAndReleasesUnusedHold()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        await Repo().ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default);
        var completed = Proposal(run); await Repo().CompleteAsync(completed, default); await Repo().CompleteAsync(completed, default);
        Assert.Equal(completed, await Repo().GetAsync(run.OwnerId, run.Id, default));
        var budget = await Repo().GetBudgetAsync(run.OwnerId); Assert.Equal(140, budget.ChargedTokens); Assert.Equal(.00036m, budget.ChargedUsd); Assert.Equal(0, budget.HeldTokens);
        Assert.Equal(2, (await Repo().GetAuditAsync(run.OwnerId, run.Id, default)).Count);
        Assert.False(await Repo().ReserveAsync(run.OwnerId, run.Id, new(1, 0), default));
    }
    [Fact] public async Task KnownOverrunIsChargedAndBlocksNextReservation()
    {
        var owner = Guid.NewGuid(); var run = New(owner); await Repo().BeginAsync(run, default);
        await Repo().ReserveAsync(owner, run.Id, new(1500, .004m), default);
        await Repo().CompleteAsync(Fail(run, new(20000, 2, true)), default);
        var next = await Repo().BeginAsync(New(owner), default);
        Assert.False(await Repo().ReserveAsync(owner, next.Id, new(1, 0), default));
        Assert.Equal(20000, (await Repo().GetBudgetAsync(owner)).ChargedTokens);
    }
    [Fact] public async Task UnknownUsageMustChargeFullReservation()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        await Repo().ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default);
        await Assert.ThrowsAsync<ArgumentException>(() => Repo().CompleteAsync(Fail(run, new(0, 0, false)), default));
        await Repo().CompleteAsync(Fail(run, new(1500, .004m, false)), default);
        Assert.Equal(1500, (await Repo().GetBudgetAsync(run.OwnerId)).ChargedTokens);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task ExpiredRunRecoveryIsDurableAndNeverReopens(bool reserved)
    {
        var owner = Guid.NewGuid(); var run = New(owner); await Repo().BeginAsync(run, default);
        if (reserved) await Repo().ReserveAsync(owner, run.Id, new(1500, .004m), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo().RecoverAsync(owner, run.Id, default));
        var repo = Repo(clock: new FutureClock());
        var recovered = await repo.RecoverAsync(owner, run.Id, default);
        Assert.Equal(AgentFailure.Interrupted, recovered!.Failure); Assert.Equal(!reserved, recovered.Accounting.UsageKnown);
        Assert.Equal(reserved ? 1500 : 0, recovered.Accounting.ChargedTokens);
        Assert.Equal(recovered, await Repo().RecoverAsync(owner, run.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Repo().CompleteAsync(Fail(run), default));
        Assert.False(await Repo().ReserveAsync(owner, run.Id, new(1, 0), default));
    }
    [Fact] public async Task RecoveryDoesNotExposeAnotherOwner()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        Assert.Null(await Repo(clock: new FutureClock()).RecoverAsync(Guid.NewGuid(), run.Id, default));
    }
    [Fact] public async Task ConcurrentReviewsCommitOnlyOneDecision()
    {
        var owner = Guid.NewGuid(); var run = New(owner); await Repo().BeginAsync(run, default);
        await Repo().ReserveAsync(owner, run.Id, new(1500, .004m), default);
        var completed = Proposal(run); await Repo().CompleteAsync(completed, default);
        var yes = completed.Review(owner, new(2, completed.Proposal!.OutputSha256, true, true, "Checked"), DateTime.UtcNow);
        var no = completed.Review(owner, new(2, completed.Proposal.OutputSha256, true, false), DateTime.UtcNow);
        var results = await Task.WhenAll(Repo().TryReviewAsync(yes, 2, default), Repo().TryReviewAsync(no, 2, default));
        Assert.Single(results, x => x);
        var final = await Repo().GetAsync(owner, run.Id, default); Assert.Equal(3, final!.Revision);
        Assert.Equal(3, (await Repo().GetAuditAsync(owner, run.Id, default)).Count);
        var metadata = string.Join("", (await Repo().GetAuditAsync(owner, run.Id, default)).Select(x => System.Text.Json.JsonSerializer.Serialize(x)));
        Assert.DoesNotContain("\"Text\"", metadata); Assert.DoesNotContain("OutputJson", metadata); Assert.DoesNotContain("Checked", metadata);
    }
    [Fact] public async Task InvalidCompletionRollsBackStateAndAudit()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        await Assert.ThrowsAsync<ArgumentException>(() => Repo().CompleteAsync(Fail(run, new(1, 1, true)), default));
        Assert.Equal(AgentRunState.Running, (await Repo().GetAsync(run.OwnerId, run.Id, default))!.State);
        Assert.Single(await Repo().GetAuditAsync(run.OwnerId, run.Id, default));
    }
    [Fact] public async Task FractionalCostsAreStoredWithoutRounding()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        var usd = .0000000000000000000000000001m;
        await Repo().ReserveAsync(run.OwnerId, run.Id, new(1, usd), default);
        await Repo().CompleteAsync(Fail(run, new(1, usd, true)), default);
        Assert.Equal(usd, (await Repo().GetBudgetAsync(run.OwnerId)).ChargedUsd);
    }
    [Fact] public async Task AuditWriteFailureRollsBackCompletionAndAccounting()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        await Repo().ReserveAsync(run.OwnerId, run.Id, new(1500, .004m), default);
        await using (var db = await Factory.CreateDbContextAsync())
        {
            db.Audits.Add(new() { RunId = run.Id, Revision = 2, Metadata = "synthetic collision" }); await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateException>(() => Repo().CompleteAsync(Proposal(run), default));
        Assert.Equal(AgentRunState.Running, (await Repo().GetAsync(run.OwnerId, run.Id, default))!.State);
        var budget = await Repo().GetBudgetAsync(run.OwnerId); Assert.Equal(1500, budget.HeldTokens); Assert.Equal(0, budget.ChargedTokens);
        await using var cleanup = await Factory.CreateDbContextAsync();
        await cleanup.Audits.Where(x => x.RunId == run.Id && x.Revision == 2).ExecuteDeleteAsync();
    }
    [Fact] public async Task PopulatedHistoryPreventsDestructiveDowngrade()
    {
        var run = New(Guid.NewGuid()); await Repo().BeginAsync(run, default);
        await using var db = await Factory.CreateDbContextAsync();
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync("0"));
        Assert.NotNull(await Repo().GetAsync(run.OwnerId, run.Id, default));
        Assert.Contains("20261016075234_InitialAgentsSchema", await db.Database.GetAppliedMigrationsAsync());
    }
    private sealed class FutureClock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddMinutes(6); }
}
