
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Domain.Enums;

namespace JobApplicationAgent.Matching.UnitTests.Domain.Entities;

public sealed class MatchingResultTests
{
    [Fact]
    public void Constructor_ShouldCreatePendingMatchingResult()
    {
        var candidateId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var result = new MatchingResult(candidateId, jobId);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(candidateId, result.CandidateProfileId);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal(MatchingStatus.Pending, result.Status);
        Assert.Null(result.OverallScore);
        Assert.Null(result.CompletedAtUtc);
        Assert.Empty(result.Criteria);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyCandidateId()
    {
        Assert.Throws<ArgumentException>(() =>
            new MatchingResult(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyJobId()
    {
        Assert.Throws<ArgumentException>(() =>
            new MatchingResult(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Complete_ShouldCalculateWeightedScore()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
            new MatchingCriterion(
                "Skills", 90m, 0.5m, "Compétences adaptées"),

            new MatchingCriterion(
                "Experience", 80m, 0.3m, "Expérience pertinente"),

            new MatchingCriterion(
                "Location", 70m, 0.2m, "Localisation compatible")
        };

        result.Complete(criteria);

        Assert.Equal(83m, result.OverallScore);
        Assert.Equal(MatchingStatus.Completed, result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(3, result.Criteria.Count);
    }

    [Fact]
    public void Complete_ShouldNormalizeWeightsOfEvaluatedCriteria()
    {
        var result = new MatchingResult(Guid.NewGuid(), Guid.NewGuid());

        result.Complete([
            new MatchingCriterion("Skills", 80m, 0.5m, "Evaluated"),
            MatchingCriterion.NotEvaluated("Experience", 0.5m, "Missing data")
        ]);

        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(MatchingStatus.Completed, result.Status);
        Assert.Equal(2, result.Criteria.Count);
    }

    [Fact]
    public void Complete_ShouldRejectEmptyCriteria()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.Throws<ArgumentException>(() =>
            result.Complete([]));
    }

    [Fact]
    public void Complete_ShouldReturnInsufficientData_WhenTotalWeightIsZero()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
            new MatchingCriterion(
                "Skills", 80m, 0m, "Test")
        };

        result.Complete(criteria);

        Assert.Equal(MatchingStatus.InsufficientData, result.Status);
        Assert.Null(result.OverallScore);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Equal(criteria, result.Criteria);
    }

    [Fact]
    public void Complete_ShouldNotAllowSecondCompletion()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
            new MatchingCriterion(
                "Skills", 80m, 1m, "Test")
        };

        result.Complete(criteria);

        Assert.Throws<InvalidOperationException>(() =>
            result.Complete(criteria));
    }

    [Fact]
    public void Fail_ShouldSetFailedStatus()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        result.Fail();

        Assert.Equal(MatchingStatus.Failed, result.Status);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Null(result.OverallScore);
    }

    [Fact]
    public void Fail_ShouldNotAllowFailureAfterCompletion()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        result.Complete(
        [
            new MatchingCriterion(
                "Skills", 90m, 1m, "Test")
        ]);

        Assert.Throws<InvalidOperationException>(() =>
            result.Fail());
    }

    [Fact]
    public void Complete_ShouldIgnoreNotEvaluatedCriteria()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
        new MatchingCriterion(
            "Skills", 80m, 0.6m, "Skills match"),

        MatchingCriterion.NotEvaluated(
            "Salary", 0.4m, "Salary information unavailable")
    };

        result.Complete(criteria);

        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(2, result.Criteria.Count);
    }


    [Fact]
    public void Complete_ShouldReturnInsufficientData_WhenNoCriterionCanBeEvaluated()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
        MatchingCriterion.NotEvaluated(
            "Salary", 0.5m, "Salary unavailable"),

        MatchingCriterion.NotEvaluated(
            "Location", 0.5m, "Location unavailable")
    };

        result.Complete(criteria);

        Assert.Equal(MatchingStatus.InsufficientData, result.Status);
        Assert.Null(result.OverallScore);
        Assert.Equal(2, result.Criteria.Count);
        Assert.NotNull(result.CompletedAtUtc);
    }


    [Fact]
    public void Complete_ShouldIncludeEvaluatedZeroScore()
    {
        var result = new MatchingResult(
            Guid.NewGuid(),
            Guid.NewGuid());

        var criteria = new[]
        {
        new MatchingCriterion(
            "Skills", 100m, 0.5m, "Full match"),

        new MatchingCriterion(
            "Contract", 0m, 0.5m, "Incompatible contract")
    };

        result.Complete(criteria);

        Assert.Equal(50m, result.OverallScore);
    }

}
