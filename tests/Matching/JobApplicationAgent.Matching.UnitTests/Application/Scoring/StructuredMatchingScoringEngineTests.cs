
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Domain.Enums;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Application.Scoring;

public sealed class StructuredMatchingScoringEngineTests
{
    private readonly StructuredMatchingScoringEngine _engine = new();

    [Fact]
    public async Task EvaluateAsync_ShouldMatchIdenticalJobTitles()
    {
        var profile = CreateProfile("Data Engineer");
        var job = CreateJob("Data Engineer");

        var criteria = await _engine.EvaluateAsync(profile, job);

        var title = Assert.Single(criteria, x => x.Name == "JobTitle");

        Assert.Equal(MatchingCriterionStatus.Evaluated, title.Status);
        Assert.Equal(100m, title.Score);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldIgnoreTitleCasingAndSpaces()
    {
        var profile = CreateProfile("  data engineer ");
        var job = CreateJob("DATA ENGINEER");

        var criteria = await _engine.EvaluateAsync(profile, job);

        var title = Assert.Single(criteria, x => x.Name == "JobTitle");

        Assert.Equal(100m, title.Score);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotEvaluateMissingTitle()
    {
        var profile = CreateProfile(null);
        var job = CreateJob("Data Engineer");

        var criteria = await _engine.EvaluateAsync(profile, job);

        var title = Assert.Single(criteria, x => x.Name == "JobTitle");

        Assert.Equal(MatchingCriterionStatus.NotEvaluated, title.Status);
        Assert.Null(title.Score);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotInventSkillRequirements()
    {
        var profile = CreateProfile("Data Engineer");
        var job = CreateJob("Data Engineer");

        var criteria = await _engine.EvaluateAsync(profile, job);

        var skills = Assert.Single(criteria, x => x.Name == "Skills");

        Assert.Equal(MatchingCriterionStatus.NotEvaluated, skills.Status);
        Assert.Null(skills.Score);
    }

    [Fact]
    public async Task EvaluateAsync_ShouldNotEvaluateMissingLocationPreferences()
    {
        var profile = CreateProfile("Data Engineer");
        var job = CreateJob("Data Engineer");

        var criteria = await _engine.EvaluateAsync(profile, job);

        var location = Assert.Single(criteria, x => x.Name == "Location");

        Assert.Equal(MatchingCriterionStatus.NotEvaluated, location.Status);
    }

    private static ProfileMatchingData CreateProfile(string? jobTitle) =>
        new(
            Guid.NewGuid(),
            jobTitle,
            null,
            [],
            [],
            [],
            [],
            [],
            null);

    private static JobMatchingData CreateJob(string title) =>
        new(
            Guid.NewGuid(),
            title,
            "Example Company",
            "Paris",
            "Remote",
            "Permanent",
            null,
            null,
            null,
            "Python and Spark");
}
