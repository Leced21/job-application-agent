using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Domain.Enums;

namespace JobApplicationAgent.Matching.UnitTests.Application.Scoring;

public sealed class ScoringRulesTests
{
    [Fact]
    public void Identifier_ShouldBeStable_AndChangeWithRulesOrWeights()
    {
        var defaults = new ScoringRules();
        Assert.Equal(defaults.Identifier, new ScoringRules
        {
            JobTitleWeight = .3000m, SkillsWeight = .5000m, LocationWeight = .2000m
        }.Identifier);
        Assert.NotEqual(defaults.Identifier, new ScoringRules
        {
            JobTitleWeight = .50m, SkillsWeight = .30m, LocationWeight = .20m
        }.Identifier);
        Assert.NotEqual(defaults.Identifier, new ScoringRules { RulesVersion = "deterministic-v3" }.Identifier);
        Assert.StartsWith("deterministic-v2:", defaults.Identifier);
        Assert.Equal(64, defaults.Identifier.Split(':')[1].Length);
    }

    [Theory]
    [InlineData(-.1, .9, .2)]
    [InlineData(1.1, 0, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(.3, .5, .3)]
    [InlineData(.30001, .49999, .2)]
    public void Validate_ShouldRejectInvalidWeights(decimal title, decimal skills, decimal location)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ScoringRules
        {
            JobTitleWeight = title, SkillsWeight = skills, LocationWeight = location
        }.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid version")]
    [InlineData("version:1")]
    [InlineData("évaluation")]
    public void Validate_ShouldRejectInvalidVersion(string version) =>
        Assert.Throws<ArgumentException>(() => new ScoringRules { RulesVersion = version }.Validate());

    [Theory]
    [InlineData(.3, .5, .2, 64.5)]
    [InlineData(.5, .3, .2, 67.5)]
    [InlineData(.6, 0, .4, 79)]
    public async Task Engine_ShouldUseConfiguredWeights_AndVersion(decimal title, decimal skills, decimal location, decimal expected)
    {
        var rules = new ScoringRules { JobTitleWeight = title, SkillsWeight = skills, LocationWeight = location };
        var engine = Engine(rules);
        var criteria = await engine.EvaluateAsync(Profile(), Job());
        var result = new MatchingResult(Guid.NewGuid(), Guid.NewGuid(), engine.Version);
        result.Complete(criteria);
        Assert.Equal(expected, result.OverallScore);
        Assert.Equal(100m, result.EvaluationCoverage);
        Assert.Equal(rules.Identifier, result.ScoringVersion);
        Assert.Equal(skills, Assert.Single(criteria, criterion => criterion.Name == "Skills").Weight);
    }

    [Fact]
    public async Task Engine_ShouldDistinguishMissingFromDisabled_WithoutCallingDisabledService()
    {
        var profile = Profile() with { Skills = [] };
        var missing = await Engine(new ScoringRules()).EvaluateAsync(profile, Job());
        Assert.Equal(MatchingCriterionStatus.NotEvaluated, Assert.Single(missing, item => item.Name == "Skills").Status);

        var engine = new StructuredMatchingScoringEngine(new JobTitleSimilarityService(), new FailingSkillService(),
            new LocationMatchingService(), new ScoringRules { JobTitleWeight = .6m, SkillsWeight = 0, LocationWeight = .4m });
        var disabled = await engine.EvaluateAsync(profile, Job());
        var skill = Assert.Single(disabled, item => item.Name == "Skills");
        Assert.Equal(MatchingCriterionStatus.NotApplicable, skill.Status);
        Assert.Null(skill.Score);
        Assert.Contains("disabled", skill.Explanation);
    }

    [Fact]
    public void Result_ShouldExcludeNotApplicableWeight_ButPenalizeMissingData()
    {
        var result = new MatchingResult(Guid.NewGuid(), Guid.NewGuid());
        result.Complete([
            new MatchingCriterion("Title", 80m, .3m, "Evaluated"),
            MatchingCriterion.NotApplicable("Skills", .5m, "Not required"),
            MatchingCriterion.NotEvaluated("Location", .2m, "Missing data")
        ]);
        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(60m, result.EvaluationCoverage);
    }

    [Fact]
    public void Result_ShouldNotInventScore_WhenEverythingIsNotApplicable()
    {
        var result = new MatchingResult(Guid.NewGuid(), Guid.NewGuid());
        result.Complete([MatchingCriterion.NotApplicable("Title", 1m, "Not applicable")]);
        Assert.Null(result.OverallScore);
        Assert.Equal(0m, result.EvaluationCoverage);
        Assert.Equal(MatchingStatus.InsufficientData, result.Status);
    }

    [Fact]
    public void Result_ShouldPreserveLegacyVersion_AndRejectInvalidVersion()
    {
        Assert.Equal("legacy-v1", new MatchingResult(Guid.NewGuid(), Guid.NewGuid()).ScoringVersion);
        Assert.Throws<ArgumentException>(() => new MatchingResult(Guid.NewGuid(), Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => new MatchingResult(Guid.NewGuid(), Guid.NewGuid(), new string('x', 129)));
    }

    private static StructuredMatchingScoringEngine Engine(ScoringRules rules) =>
        new(new JobTitleSimilarityService(), new SkillMatchingService(), new LocationMatchingService(), rules);
    private static ProfileMatchingData Profile() => new(Guid.NewGuid(), "Data Engineer", null, [],
        [new MatchingSkillData("Python", null, null, null)], [], [], [],
        new MatchingPreferencesData([], ["Paris"], [], ["Remote"], null, null, null));
    private static JobMatchingData Job() => new(Guid.NewGuid(), "Data Analyst", "Company", "Paris", "Remote",
        "Permanent", null, null, null, "Python and Spark");

    private sealed class FailingSkillService : JobApplicationAgent.Matching.Application.Abstractions.ISkillMatchingService
    {
        public SkillMatchingResult Evaluate(IReadOnlyCollection<string> skills, string? description) =>
            throw new InvalidOperationException("Disabled criteria must not be evaluated.");
    }
}
