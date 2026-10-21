using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.IntegrationTests.Infrastructure;
using NSubstitute;

namespace JobApplicationAgent.Matching.IntegrationTests.Api;

public sealed class ScoringRulesContractTests
{
    [Theory]
    [InlineData(false, 64.5)]
    [InlineData(true, 79)]
    public async Task CalculateAndRecommendations_ShouldExposeConfiguredVersionAndScores(bool disableSkills, decimal expectedScore)
    {
        using var factory = Factory(disableSkills);
        using var client = factory.CreateClient();
        var calculation = await client.PostAsJsonAsync("/api/v1/matching/calculate", new { jobId = factory.Job!.Id });
        calculation.EnsureSuccessStatusCode();
        var result = await calculation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.ScoringRules.Identifier, result.GetProperty("scoringVersion").GetString());
        Assert.Equal(expectedScore, result.GetProperty("overallScore").GetDecimal());
        Assert.Equal(100m, result.GetProperty("evaluationCoverage").GetDecimal());
        var skill = Assert.Single(result.GetProperty("criteria").EnumerateArray(),
            criterion => criterion.GetProperty("name").GetString() == "Skills");
        Assert.Equal(disableSkills ? "NotApplicable" : "Evaluated", skill.GetProperty("status").GetString());
        await factory.MatchingRepository.Received(1).AddAsync(
            Arg.Is<MatchingResult>(matching => matching.ScoringVersion == factory.ScoringRules.Identifier),
            Arg.Any<CancellationToken>());

        var recommendations = await client.GetFromJsonAsync<JsonElement>("/api/v1/matching/recommendations");
        Assert.Equal(factory.ScoringRules.Identifier, recommendations.GetProperty("scoringVersion").GetString());
        var item = Assert.Single(recommendations.GetProperty("items").EnumerateArray());
        Assert.Equal(factory.ScoringRules.Identifier, item.GetProperty("scoringVersion").GetString());
        Assert.Equal(expectedScore, item.GetProperty("rankingScore").GetDecimal());
        var recommendationSkill = Assert.Single(item.GetProperty("criteria").EnumerateArray(),
            criterion => criterion.GetProperty("name").GetString() == "Skills");
        Assert.Equal(disableSkills ? 2 : 0, recommendationSkill.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Recommendations_ShouldReportCurrentRules_EvenWhenCatalogueIsEmpty()
    {
        using var factory = Factory(false);
        factory.Jobs = [];
        using var client = factory.CreateClient();
        var result = await client.GetFromJsonAsync<JsonElement>("/api/v1/matching/recommendations");
        Assert.Equal(factory.ScoringRules.Identifier, result.GetProperty("scoringVersion").GetString());
        Assert.Equal(0, result.GetProperty("totalCount").GetInt32());
        Assert.Empty(result.GetProperty("items").EnumerateArray());
    }

    private static MatchingApiFactory Factory(bool disableSkills) => new()
    {
        UseRealScoring = true,
        ScoringRules = disableSkills
            ? new ScoringRules { JobTitleWeight = .6m, SkillsWeight = 0m, LocationWeight = .4m }
            : new ScoringRules(),
        Profile = new ProfileMatchingData(Guid.NewGuid(), "Data Engineer", null, [],
            [new MatchingSkillData("Python", null, null, null)], [], [], [],
            new MatchingPreferencesData([], ["Paris"], [], ["Remote"], null, null, null)),
        Job = new JobMatchingData(Guid.NewGuid(), "Data Analyst", "Company", "Paris", "Remote", "Permanent",
            null, null, null, "Python and Spark")
    };
}
