using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public class AgentReviewTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExactHumanDecisionIsFinalAndDoesNotReinvokeModel(bool approved)
    {
        var f = new AgentFixture(); var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        var review = new AgentReviewService(f.Runs, TimeProvider.System);
        var decision = await review.ReviewAsync(f.Owner, run.Id, new(run.Revision, run.Proposal!.OutputSha256, true, approved, "Checked source"));
        Assert.Equal(approved ? AgentRunState.Approved : AgentRunState.Rejected, decision!.State);
        Assert.Equal(run.Revision + 1, decision.Revision);
        Assert.Equal(decision, await f.Engine.ExecuteAsync(f.Owner, f.Command)); Assert.Equal(1, f.Model.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => review.ReviewAsync(f.Owner, run.Id, new(decision.Revision, run.Proposal.OutputSha256, true, approved)));
    }
    [Theory]
    [InlineData("confirmation")]
    [InlineData("revision")]
    [InlineData("hash")]
    public async Task InexactReviewCannotApprove(string mismatch)
    {
        var f = new AgentFixture(); var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        var command = new AgentReview(mismatch == "revision" ? 0 : run.Revision, mismatch == "hash" ? new string('0', 64) : run.Proposal!.OutputSha256, mismatch != "confirmation", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AgentReviewService(f.Runs, TimeProvider.System).ReviewAsync(f.Owner, run.Id, command));
        Assert.Equal(AgentRunState.PendingReview, (await f.Runs.GetAsync(f.Owner, run.Id, default))!.State);
    }
    [Fact]
    public async Task ForeignOwnerCannotReadOrReview_AndRequestKeysAreOwnerScoped()
    {
        var f = new AgentFixture(); var run = await f.Engine.ExecuteAsync(f.Owner, f.Command); var foreign = Guid.NewGuid();
        Assert.Null(await new AgentReviewService(f.Runs, TimeProvider.System).ReviewAsync(foreign, run.Id, new(2, run.Proposal!.OutputSha256, true, true)));
        Assert.Throws<UnauthorizedAccessException>(() => run.Review(foreign, new(2, run.Proposal.OutputSha256, true, true), DateTime.UtcNow));
        f.Source.Value = f.Source.Value! with { OwnerId = foreign };
        var second = await f.Engine.ExecuteAsync(foreign, f.Command); Assert.NotEqual(run.Id, second.Id);
    }
    [Fact]
    public async Task ConcurrentHumanDecisionsHaveOneWinner()
    {
        var f = new AgentFixture(); var run = await f.Engine.ExecuteAsync(f.Owner, f.Command); var review = new AgentReviewService(f.Runs, TimeProvider.System);
        var allRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var readers = 0;
        f.Runs.BeforeReviewCommit = () => { if (Interlocked.Increment(ref readers) == 8) allRead.SetResult(); return allRead.Task; };
        var decisions = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i => {
            try { await review.ReviewAsync(f.Owner, run.Id, new(2, run.Proposal!.OutputSha256, true, i % 2 == 0)); return true; }
            catch (InvalidOperationException) { return false; }
        }));
        Assert.Single(decisions, x => x); Assert.Equal(2, f.Runs.Audit.Count);
    }
    [Fact]
    public async Task ALocalModelCanHaveZeroMonetaryCostButStillConsumeTokenQuota()
    {
        var f = new AgentFixture(); f.Policy = f.Policy with { MaxCostUsd = 0, InputUsdPerMillion = 0, OutputUsdPerMillion = 0 };
        var run = await f.Engine.ExecuteAsync(f.Owner, f.Command);
        Assert.Equal(AgentRunState.PendingReview, run.State); Assert.Equal(0, run.Accounting.ChargedUsd); Assert.Equal(140, run.Accounting.ChargedTokens);
    }
    [Theory]
    [InlineData("model")]
    [InlineData("input")]
    [InlineData("output")]
    [InlineData("cost")]
    [InlineData("timeout")]
    public async Task InvalidServerPolicyNeverBeginsARequest(string invalid)
    {
        var f = new AgentFixture(); f.Policy = invalid switch {
            "model" => f.Policy with { ModelId = "" }, "input" => f.Policy with { MaxInputTokens = 0 }, "output" => f.Policy with { MaxOutputTokens = 4097 },
            "cost" => f.Policy with { MaxCostUsd = -1 }, _ => f.Policy with { Timeout = TimeSpan.Zero } };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Engine.ExecuteAsync(f.Owner, f.Command)); Assert.Equal(0, f.Source.Calls);
    }
}
