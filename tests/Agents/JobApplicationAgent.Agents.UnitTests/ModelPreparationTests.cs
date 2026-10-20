using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public sealed class ModelPreparationTests
{
    [Fact] public async Task PreparationIgnoringCancellationCannotBlockOrReserveBudget()
    {
        var f = new AgentFixture(); f.Policy = f.Policy with { Timeout = TimeSpan.FromMilliseconds(100) };
        var delayed = new TaskCompletionSource<PreparedModelInput>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Model.Prepare = (_, _, _) => delayed.Task;
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.TimedOut, run.Failure); Assert.Equal(0, run.Accounting.ChargedTokens); Assert.True(run.Accounting.UsageKnown);
        Assert.Equal(0, f.Model.Calls); Assert.Equal(100000, f.Runs.AvailableTokens);
        delayed.SetResult(new(f.Policy.ModelId, JobAnalysisPrompt.Build(f.Source.Value!), 100));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task PreparationCannotChangeModelOrPrompt(bool changeModel)
    {
        var f = new AgentFixture();
        f.Model.Prepare = (id, prompt, _) => Task.FromResult(new PreparedModelInput(changeModel ? "other" : id,
            changeModel ? prompt : prompt with { System = "modified" }, 100));
        var result = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.ModelUnavailable, result.Failure); Assert.Equal(0, f.Model.Calls); Assert.Equal(100000, f.Runs.AvailableTokens);
    }
    [Fact] public async Task ProvenNoGenerationReleasesReservationWithoutInventedUsage()
    {
        var f = new AgentFixture(); f.Model.Respond = (_, _) => throw new AgentModelNotInvokedException();
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentFailure.ModelUnavailable, run.Failure); Assert.Equal(0, run.Accounting.ChargedTokens);
        Assert.True(run.Accounting.UsageKnown); Assert.Equal(100000, f.Runs.AvailableTokens);
    }
    [Theory] [InlineData(AgentFailure.InvalidOutput)] [InlineData(AgentFailure.UsageViolation)]
    public async Task ProviderRejectsOutputButPreservesKnownUsage(AgentFailure failure)
    {
        var f = new AgentFixture(); f.Model.Respond = (_, _) => Task.FromResult(new ModelResponse("", 100, 40, failure));
        var result = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(failure, result.Failure); Assert.Null(result.Proposal); Assert.True(result.Accounting.UsageKnown);
        Assert.Equal(140, result.Accounting.ChargedTokens); Assert.Equal(.00036m, result.Accounting.ChargedUsd);
    }
}
