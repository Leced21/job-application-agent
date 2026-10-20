using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public sealed class MatchingEnrichmentTests
{
    private static string Source(string status = "Evaluated", decimal? score = 50, decimal weight = 1) =>
        MatchingEnrichmentPrompt.Freeze("rules-v1", score, score.HasValue ? 100 : 0, [new("Skills", status, score, weight)]);
    private static string Output(string focus = "PartialMatchReported", string criterion = "Skills", string priority = "High") =>
        JsonSerializer.Serialize(new { schemaVersion = "matching-enrichment-v1", observations = new[] { new { criterion, focus, priority } } });
    [Theory]
    [InlineData(100, "MatchReported")] [InlineData(50, "PartialMatchReported")] [InlineData(0, "NoMatchReported")]
    public void ObservationMustDescribeTheActualScore(int score, string focus)
    {
        Assert.Equal(Output(focus), MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output(focus), Source(score: score)));
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output("InformationMissing"), Source(score: score)));
    }
    [Fact] public void UnassessedMeansMissingInformationNeverMissingQualifications()
    {
        Assert.Equal(Output("InformationMissing"), MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output("InformationMissing"), Source("NotEvaluated", null)));
        Assert.Contains("sans supposer", MatchingEnrichmentPrompt.Messages["InformationMissing"]);
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output("NoMatchReported"), Source("NotEvaluated", null)));
    }
    [Theory] [InlineData("NotApplicable", 1)] [InlineData("Evaluated", 0)]
    public void InactiveCriteriaCannotBeSelected(string status, int weight)
    {
        var source = Source(status, status == "Evaluated" ? 50 : null, weight);
        var empty = """{"schemaVersion":"matching-enrichment-v1","observations":[]}""";
        Assert.Equal(empty, MatchingEnrichmentPrompt.ValidateAndCanonicalize(empty, source));
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output(), source));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[]}")]
    [InlineData("{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[{\"criterion\":\"Skills\",\"focus\":\"PartialMatchReported\",\"priority\":\"High\",\"score\":100}]}")]
    [InlineData("{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[{\"criterion\":\"Skills\",\"focus\":\"PartialMatchReported\",\"priority\":\"High\",\"explanation\":\"Invented experience\"}]}")]
    [InlineData("{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[{\"criterion\":\"Skills\",\"criterion\":\"Location\",\"focus\":\"PartialMatchReported\",\"priority\":\"High\"}]}")]
    [InlineData("{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[],\"tool_calls\":[]}")]
    [InlineData("```json\n{}\n```")]
    public void RejectsFreeTextScoreChangesDuplicatesActionsAndEmptyUnjustifiedOutput(string output) =>
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(output, Source()));
    [Theory] [InlineData("PartialMatchReported", "Email", "High")] [InlineData("PartialMatchReported", "Location", "High")]
    [InlineData("PartialMatchReported", "Skills", "Urgent")] [InlineData("Invented", "Skills", "High")]
    public void OnlyKnownPresentCriteriaFocusAndPriorityAreAllowed(string focus, string criterion, string priority) =>
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(Output(focus, criterion, priority), Source()));
    [Fact] public void DuplicateObservationsAndOversizedOutputAreRejected()
    {
        var item = """{"criterion":"Skills","focus":"PartialMatchReported","priority":"High"}""";
        foreach (var count in new[] { 2, 4 }) Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(
            "{\"schemaVersion\":\"matching-enrichment-v1\",\"observations\":[" + string.Join(',', Enumerable.Repeat(item, count)) + "]}", Source()));
        Assert.Throws<InvalidAgentOutputException>(() => MatchingEnrichmentPrompt.ValidateAndCanonicalize(new string(' ', 8193), Source()));
    }
    [Theory] [InlineData("Unknown", 50, 1)] [InlineData("NotEvaluated", 50, 1)] [InlineData("Evaluated", 101, 1)]
    [InlineData("Evaluated", -1, 1)] [InlineData("Evaluated", 50, 2)]
    public void InvalidSignalsFailBeforeModel(string status, int score, int weight) =>
        Assert.Throws<InvalidAgentSourceException>(() => Source(status, score, weight));
    [Fact] public void SourceRequiresExactFieldsAndKnownCriteria()
    {
        Assert.Throws<InvalidAgentSourceException>(() => MatchingEnrichmentPrompt.Read(Source().Replace("\"Skills\"", "\"Private skill\"")));
        Assert.Throws<InvalidAgentSourceException>(() => MatchingEnrichmentPrompt.Read(Source().Replace("\"weight\":1", "\"weight\":1,\"weight\":0")));
        Assert.Throws<InvalidAgentSourceException>(() => MatchingEnrichmentPrompt.Read(Source().Replace("\"criteria\":", "\"email\":\"private@example.test\",\"criteria\":")));
    }
    [Fact] public async Task UsesMatchingPromptAccountingReplayAndExactHumanReview()
    {
        var f = new AgentFixture(); var text = Source();
        f.Source.Value = new(f.Owner, f.Command.ResourceId, text, AgentRun.Hash(text));
        f.Model.Respond = (_, _) => Task.FromResult(new ModelResponse(Output(), 100, 40));
        var command = f.Command with { Kind = AgentKind.MatchingEnrichment };
        var run = await f.Engine.ExecuteAsync(f.Owner, command);
        Assert.Equal(AgentRunState.PendingReview, run.State); Assert.Equal(MatchingEnrichmentPrompt.Version, run.Proposal!.PromptVersion);
        Assert.Equal(140, run.Accounting.ChargedTokens); Assert.Contains("eligibleObservations", f.Model.LastRequest!.Prompt.UntrustedDataJson);
        var reviewed = await new AgentReviewService(f.Runs, TimeProvider.System).ReviewAsync(f.Owner, run.Id, new(2, run.Proposal.OutputSha256, true, true));
        Assert.Equal(AgentRunState.Approved, reviewed!.State); Assert.Equal(reviewed, await f.Engine.ExecuteAsync(f.Owner, command)); Assert.Equal(1, f.Model.Calls);
    }
    [Fact] public async Task RawTextSourceFailsBeforeTokenizerAndJobOutputCannotBeReused()
    {
        var f = new AgentFixture(); var command = f.Command with { Kind = AgentKind.MatchingEnrichment };
        var bad = await f.Engine.ExecuteAsync(f.Owner, command); Assert.Equal(AgentFailure.SourceRejected, bad.Failure); Assert.Equal(0, f.Model.Calls);
        var text = Source(); f.Source.Value = new(f.Owner, command.ResourceId, text, AgentRun.Hash(text));
        var wrongSchema = await f.Engine.ExecuteAsync(f.Owner, command with { RequestId = Guid.NewGuid() });
        Assert.Equal(AgentFailure.InvalidOutput, wrongSchema.Failure); Assert.Equal(140, wrongSchema.Accounting.ChargedTokens);
    }
}
