using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class MatchingResultRepositoryTests
    : IClassFixture<MatchingPostgresFixture>
{
    private readonly MatchingPostgresFixture _fixture;

    public MatchingResultRepositoryTests(
        MatchingPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistMatchingAndCriteria()
    {
        // Arrange
        await using var dbContext = _fixture.CreateDbContext();

        var repository = new MatchingResultRepository(dbContext);

        var matching = CreateCompletedMatching();

        // Act
        await repository.AddAsync(matching);

        // Assert
        var saved = await repository.GetByIdAsync(matching.Id);

        Assert.NotNull(saved);
        Assert.Equal(matching.Id, saved.Id);
        Assert.Equal(matching.CandidateProfileId, saved.CandidateProfileId);
        Assert.Equal(matching.JobId, saved.JobId);
        Assert.Equal("Completed", saved.Status);
        Assert.Equal(90m, saved.OverallScore);
        Assert.Equal(100m, saved.EvaluationCoverage);
        Assert.Equal(3, saved.Criteria.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnCriteriaWithCorrectScores()
    {
        // Arrange
        await using var dbContext = _fixture.CreateDbContext();

        var repository = new MatchingResultRepository(dbContext);

        var matching = CreateCompletedMatching();

        await repository.AddAsync(matching);

        // Act
        var result = await repository.GetByIdAsync(matching.Id);

        // Assert
        Assert.NotNull(result);

        var criteria = result.Criteria.ToDictionary(
            criterion => criterion.Name);

        Assert.Equal(3, criteria.Count);

        Assert.Equal(100m, criteria["JobTitle"].Score);
        Assert.Equal(0.30m, criteria["JobTitle"].Weight);

        Assert.Equal(80m, criteria["Skills"].Score);
        Assert.Equal(0.50m, criteria["Skills"].Weight);

        Assert.Equal(100m, criteria["Location"].Score);
        Assert.Equal(0.20m, criteria["Location"].Weight);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByCandidateProfileId()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new MatchingResultRepository(dbContext);

        var matching = CreateCompletedMatching();
        await repository.AddAsync(matching);

        var filter = new MatchingResultFilter(
            matching.CandidateProfileId,
            null,
            null);

        var result = await repository.SearchAsync(filter);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(matching.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByJobId()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new MatchingResultRepository(dbContext);

        var matching = CreateCompletedMatching();
        await repository.AddAsync(matching);

        var filter = new MatchingResultFilter(
            null,
            matching.JobId,
            null);

        var result = await repository.SearchAsync(filter);

        Assert.Single(result.Items);
        Assert.Equal(matching.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmptyPage_WhenNoMatchingExists()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new MatchingResultRepository(dbContext);

        var filter = new MatchingResultFilter(
            Guid.NewGuid(),
            null,
            null);

        var result = await repository.SearchAsync(filter);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_ShouldPaginateResults()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new MatchingResultRepository(dbContext);

        var candidateProfileId = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            var matching = new MatchingResult(
                candidateProfileId,
                Guid.NewGuid());

            matching.Complete(
            [
                new MatchingCriterion(
                "JobTitle",
                100m,
                1m,
                "Job title matches.")
            ]);

            await repository.AddAsync(matching);
        }

        var firstPage = await repository.SearchAsync(
            new MatchingResultFilter(
                candidateProfileId,
                null,
                null,
                Page: 1,
                PageSize: 2));

        var secondPage = await repository.SearchAsync(
            new MatchingResultFilter(
                candidateProfileId,
                null,
                null,
                Page: 2,
                PageSize: 2));

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);

        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);

        var firstPageIds = firstPage.Items.Select(x => x.Id).ToHashSet();
        var secondPageId = secondPage.Items.Single().Id;

        Assert.DoesNotContain(secondPageId, firstPageIds);
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByStatus()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var repository = new MatchingResultRepository(dbContext);

        var candidateProfileId = Guid.NewGuid();

        var completed = new MatchingResult(
            candidateProfileId,
            Guid.NewGuid());

        completed.Complete(
        [
            new MatchingCriterion(
            "JobTitle",
            100m,
            1m,
            "Job title matches.")
        ]);

        var insufficient = new MatchingResult(
            candidateProfileId,
            Guid.NewGuid());

        insufficient.Complete(
        [
            MatchingCriterion.NotEvaluated(
            "JobTitle",
            1m,
            "Job title information is missing.")
        ]);

        await repository.AddAsync(completed);
        await repository.AddAsync(insufficient);

        var result = await repository.SearchAsync(
            new MatchingResultFilter(
                candidateProfileId,
                null,
                "Completed"));

        Assert.Single(result.Items);
        Assert.Equal(completed.Id, result.Items.Single().Id);
        Assert.Equal("Completed", result.Items.Single().Status);
    }


    private static MatchingResult CreateCompletedMatching()
    {
        var matching = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        matching.Complete(
        [
            new MatchingCriterion(
                "JobTitle",
                100m,
                0.30m,
                "Job title matches."),

            new MatchingCriterion(
                "Skills",
                80m,
                0.50m,
                "Four out of five skills match."),

            new MatchingCriterion(
                "Location",
                100m,
                0.20m,
                "Location and work mode match.")
        ]);

        return matching;
    }
}
