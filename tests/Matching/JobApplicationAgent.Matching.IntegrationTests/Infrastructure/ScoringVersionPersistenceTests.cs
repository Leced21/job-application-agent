using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class ScoringVersionPersistenceTests(MatchingPostgresFixture fixture) : IClassFixture<MatchingPostgresFixture>
{
    [Fact]
    public async Task Repository_ShouldPersistVersionAndNotApplicable_AndReturnVersionInSearch()
    {
        await using var db = fixture.CreateDbContext();
        var rules = new ScoringRules { JobTitleWeight = .5m, SkillsWeight = .5m, LocationWeight = 0m };
        var result = new MatchingResult(Guid.NewGuid(), Guid.NewGuid(), rules.Identifier);
        result.Complete([
            new MatchingCriterion("Title", 80m, .5m, "Evaluated"),
            MatchingCriterion.NotApplicable("Skills", .5m, "Not required for this evaluation")
        ]);
        var repository = new MatchingResultRepository(db);

        await repository.AddAsync(result);
        db.ChangeTracker.Clear();
        var saved = await repository.GetByIdAsync(result.Id);
        var page = await repository.SearchAsync(new MatchingResultFilter(result.CandidateProfileId, null, null, 1, 20));

        Assert.Equal(rules.Identifier, saved!.ScoringVersion);
        Assert.Equal(80m, saved.OverallScore);
        Assert.Equal(100m, saved.EvaluationCoverage);
        var skill = Assert.Single(saved.Criteria, criterion => criterion.Name == "Skills");
        Assert.Equal("NotApplicable", skill.Status);
        Assert.Null(skill.Score);
        Assert.Equal(.5m, skill.Weight);
        Assert.Equal(rules.Identifier, Assert.Single(page.Items).ScoringVersion);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Migration_ShouldUpgradeExistingRowsAsLegacy_WithoutChangingScores()
    {
        // This fixture has its own disposable PostgreSQL container, never the local development database.
        await using var db = fixture.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261014125328_InitialMatchingSchema");
        var id = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO matching_results ("Id", "CandidateProfileId", "JobId", "Status", "OverallScore", "EvaluationCoverage", "CreatedAtUtc")
            VALUES ({id}, {profileId}, {jobId}, 'Completed', 80, 50, {created})
            """);

        await migrator.MigrateAsync();
        var result = await new MatchingResultRepository(db).GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("legacy-v1", result.ScoringVersion);
        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(50m, result.EvaluationCoverage);
        Assert.Equal(jobId, result.JobId);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
