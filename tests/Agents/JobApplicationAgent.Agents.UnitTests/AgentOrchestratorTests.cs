using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public class AgentOrchestratorTests
{
    [Fact]
    public async Task ValidOutputBecomesPendingReview_WithFrozenSourcesHashesAndActualAccounting()
    {
        var f = new AgentFixture(); var result = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentRunState.PendingReview, result.State); Assert.Null(result.Failure);
        Assert.Equal(AgentRun.Hash(result.Proposal!.OutputJson), result.Proposal.OutputSha256);
        Assert.Equal(AgentRun.Hash(result.Proposal.Source.Text), result.Proposal.Source.Sha256);
        Assert.Equal(JobAnalysisPrompt.Version, result.Proposal.PromptVersion);
        Assert.Equal(140, result.Accounting.ChargedTokens); Assert.Equal(.00036m, result.Accounting.ChargedUsd); Assert.True(result.Accounting.UsageKnown);
        Assert.Equal(100000 - 140, f.Runs.AvailableTokens); Assert.Equal(100m - .00036m, f.Runs.AvailableUsd);
        Assert.Equal(500, f.Model.LastRequest!.MaxOutputTokens); Assert.Equal(1000, f.Model.LastRequest.MaxInputTokens);
        Assert.Single(f.Runs.Audit);
        var audit = System.Text.Json.JsonSerializer.Serialize(f.Runs.Audit);
        Assert.DoesNotContain(f.Source.Value!.Text, audit);
        Assert.Equal(result.Trace!.PromptSha256, result.Proposal.PromptSha256);
    }
    [Fact]
    public async Task CompletedRetryNeverRecontactsSourceOrModel_AndConflictingIdentityIsRejected()
    {
        var f = new AgentFixture(); var first = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        f.Source.Error = new Exception("Offline");
        Assert.Equal(first, await f.Engine.ExecuteAsync(f.Owner, f.Command)); Assert.Equal(1, f.Model.Calls); Assert.Equal(1, f.Source.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Engine.ExecuteAsync(f.Owner, f.Command with { ResourceId = Guid.NewGuid() }));
    }
    [Fact]
    public async Task ConcurrentSameRequestHasOneModelInvocation()
    {
        var f = new AgentFixture(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ModelResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Model.Respond = (_, _) => { entered.SetResult(); return release.Task; };
        var first = f.Engine.ExecuteAsync(f.Owner, f.Command); await entered.Task;
        await Assert.ThrowsAsync<AgentRunInProgressException>(() => f.Engine.ExecuteAsync(f.Owner, f.Command));
        release.SetResult(new(AgentFixture.Output(), 100, 40)); await first;
        Assert.Equal(1, f.Model.Calls); Assert.Single(f.Runs.Audit);
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("resource")]
    [InlineData("hash")]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("unicode")]
    public async Task InvalidSourceNeverReachesModel(string invalid)
    {
        var f = new AgentFixture(); var source = f.Source.Value!;
        f.Source.Value = invalid switch {
            "owner" => source with { OwnerId = Guid.NewGuid() }, "resource" => source with { ResourceId = Guid.NewGuid() },
            "hash" => source with { Sha256 = new string('0', 64) }, "empty" => source with { Text = " " },
            "unicode" => source with { Text = "\ud800" }, _ => source with { Text = new string('a', 16001) } };
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.SourceRejected, run.Failure); Assert.Equal(0, f.Model.Calls); Assert.Equal(0, run.Accounting.ChargedUsd);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task InputTokenGatePreventsModelCall(int count)
    {
        var f = new AgentFixture(); f.Model.Count = count;
        Assert.Equal(AgentFailure.InputLimit, (await f.Engine.ExecuteAsync(f.Owner, f.Command)).Failure); Assert.Equal(0, f.Model.Calls);
    }
    [Theory]
    [InlineData("policy")]
    [InlineData("cost-quota")]
    [InlineData("token-quota")]
    public async Task ReservesWorstCaseBudgetBeforeInvokingModel(string reason)
    {
        var f = new AgentFixture();
        if (reason == "policy") f.Policy = f.Policy with { MaxCostUsd = .0001m };
        if (reason == "cost-quota") f.Runs.AvailableUsd = .0001m;
        if (reason == "token-quota") f.Runs.AvailableTokens = 100;
        var result = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.BudgetRejected, result.Failure); Assert.Equal(0, f.Model.Calls); Assert.Equal(0, result.Accounting.ChargedTokens);
    }
    [Fact]
    public async Task LostReservationAcknowledgementDoesNotInventModelCost()
    {
        var f = new AgentFixture(); f.Runs.LoseReservationAck = true;
        var result = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.BudgetUnavailable, result.Failure); Assert.Equal(0, f.Model.Calls);
        Assert.Equal(100m, f.Runs.AvailableUsd); Assert.True(result.Accounting.UsageKnown);
    }
    [Fact]
    public async Task TimeoutOfIgnoringModelStillFinishesAndChargesUnknownReservation()
    {
        var f = new AgentFixture(); f.Policy = f.Policy with { Timeout = TimeSpan.FromMilliseconds(100) };
        var delayed = new TaskCompletionSource<ModelResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Model.Respond = (_, _) => delayed.Task;
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.TimedOut, run.Failure); Assert.False(run.Accounting.UsageKnown);
        Assert.Equal(1500, run.Accounting.ChargedTokens); Assert.Equal(.004m, run.Accounting.ChargedUsd);
        delayed.SetResult(new(AgentFixture.Output(), 100, 40));
        Assert.Equal(run, await f.Engine.ExecuteAsync(f.Owner, f.Command)); Assert.Single(f.Runs.Audit);
    }
    [Fact]
    public async Task CallerCancellationIsFinalizedWithoutExposingProposal()
    {
        var f = new AgentFixture(); using var caller = new CancellationTokenSource();
        f.Model.Respond = (_, _) => { caller.Cancel(); return Task.FromResult(new ModelResponse(AgentFixture.Output(), 100, 40)); };
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command, caller.Token);
        Assert.Equal(AgentFailure.Cancelled, run.Failure); Assert.Null(run.Proposal); Assert.Single(f.Runs.Audit);
    }
    [Fact]
    public async Task ModelFailureDoesNotLeakAdapterErrorAndChargesMaximum()
    {
        var f = new AgentFixture(); f.Model.Respond = (_, _) => throw new Exception("SECRET_CV_AND_TOKEN");
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.ModelUnavailable, run.Failure); Assert.Equal(.004m, run.Accounting.ChargedUsd);
        Assert.DoesNotContain("SECRET", System.Text.Json.JsonSerializer.Serialize(run));
    }
    [Theory]
    [InlineData(-1, 40, false)]
    [InlineData(100, -1, false)]
    [InlineData(1001, 40, true)]
    [InlineData(100, 501, true)]
    public async Task UsageViolationRejectsOutputAndPreservesKnownActualCost(int input, int output, bool known)
    {
        var f = new AgentFixture(); f.Model.Respond = (_, _) => Task.FromResult(new ModelResponse(AgentFixture.Output(), input, output));
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.UsageViolation, run.Failure); Assert.Equal(known, run.Accounting.UsageKnown); Assert.Null(run.Proposal);
        if (known) Assert.Equal(f.Policy.Cost(input, output), run.Accounting.ChargedUsd);
    }
    [Fact]
    public async Task InvalidOutputStillAccountsKnownModelUsage()
    {
        var f = new AgentFixture(); f.Model.Respond = (_, _) => Task.FromResult(new ModelResponse("{\"actions\":[\"send\"]}", 100, 40));
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.InvalidOutput, run.Failure); Assert.True(run.Accounting.UsageKnown); Assert.Equal(.00036m, run.Accounting.ChargedUsd);
    }
    [Fact]
    public async Task ConcurrentDifferentRequestsCannotSpendTheSameReservedQuota()
    {
        var f = new AgentFixture(); f.Runs.AvailableTokens = 1500; f.Runs.AvailableUsd = .004m;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ModelResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Model.Respond = (_, _) => { entered.SetResult(); return release.Task; };
        var first = f.Engine.ExecuteAsync(f.Owner, f.Command); await entered.Task;
        var second = await f.Engine.ExecuteAsync(f.Owner, f.Command with { RequestId = Guid.NewGuid() });
        Assert.Equal(AgentFailure.BudgetRejected, second.Failure); Assert.Equal(1, f.Model.Calls);
        release.SetResult(new(AgentFixture.Output(), 100, 40)); await first;
        Assert.Equal(1360, f.Runs.AvailableTokens); Assert.Equal(.004m - .00036m, f.Runs.AvailableUsd);
    }
    [Fact]
    public async Task SourceFailureIsSanitizedAndNeverSpendsModelBudget()
    {
        var f = new AgentFixture(); f.Source.Error = new IOException("PRIVATE_SOURCE_AND_TOKEN");
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.SourceUnavailable, run.Failure); Assert.Equal(0, f.Model.Calls);
        Assert.Equal(0, run.Accounting.ChargedUsd);
        Assert.DoesNotContain("PRIVATE", System.Text.Json.JsonSerializer.Serialize(run));
    }
    [Fact]
    public async Task AuditFailureDoesNotReturnAReviewableResult()
    {
        var f = new AgentFixture(); f.Runs.FailCompletion = true;
        await Assert.ThrowsAsync<AgentPersistenceUnavailableException>(() => f.Engine.ExecuteAsync(f.Owner, f.Command));
        Assert.Empty(f.Runs.Audit);
    }
}
