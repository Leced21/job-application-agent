
using System.Net;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Domain.Enums;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class CalculateMatchingHandlerTests
{
    private static readonly Guid JobId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ShouldReturnCompletedResult_WhenDataExists()
    {
        var scoringEngine = new FakeScoringEngine();

        var handler = CreateHandler(
            CreateJob(),
            CreateProfile(),
            scoringEngine);

        var result = await handler.HandleAsync(JobId);

        Assert.NotNull(result);
        Assert.Equal(MatchingStatus.Completed, result.Status);
        Assert.Equal(JobId, result.JobId);
        Assert.Equal(ProfileId, result.CandidateProfileId);
        Assert.Equal(80m, result.OverallScore);
        Assert.Single(result.Criteria);
        Assert.True(scoringEngine.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var scoringEngine = new FakeScoringEngine();

        var handler = CreateHandler(
            null,
            CreateProfile(),
            scoringEngine);

        var result = await handler.HandleAsync(JobId);

        Assert.Null(result);
        Assert.False(scoringEngine.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenProfileDoesNotExist()
    {
        var scoringEngine = new FakeScoringEngine();

        var handler = CreateHandler(
            CreateJob(),
            null,
            scoringEngine);

        var result = await handler.HandleAsync(JobId);

        Assert.Null(result);
        Assert.False(scoringEngine.WasCalled);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectEmptyJobId()
    {
        var handler = CreateHandler(
            CreateJob(),
            CreateProfile(),
            new FakeScoringEngine());

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(Guid.Empty));
    }

    [Fact]
    public async Task HandleAsync_ShouldPropagateJobServiceErrors()
    {
        var handler = new CalculateMatchingHandler(
            new FakeProfileClient(CreateProfile()),
            new FakeJobClient(
                null,
                new HttpRequestException(
                    "Job Service unavailable",
                    null,
                    HttpStatusCode.ServiceUnavailable)),
            new FakeScoringEngine());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => handler.HandleAsync(JobId));
    }

    private static CalculateMatchingHandler CreateHandler(
        JobMatchingData? job,
        ProfileMatchingData? profile,
        IMatchingScoringEngine scoringEngine)
    {
        return new CalculateMatchingHandler(
            new FakeProfileClient(profile),
            new FakeJobClient(job),
            scoringEngine);
    }

    private static JobMatchingData CreateJob() =>
        new(
            JobId,
            "Data Engineer",
            "Example Company",
            "Paris",
            "Remote",
            "Permanent",
            50000m,
            65000m,
            "EUR",
            "Python, Spark and Databricks");

    private static ProfileMatchingData CreateProfile() =>
        new(
            ProfileId,
            "Data Engineer",
            "Experience with Spark and Python",
            [],
            [],
            [],
            [],
            [],
            null);

    private sealed class FakeProfileClient(
        ProfileMatchingData? profile) : IProfileMatchingDataClient
    {
        public Task<ProfileMatchingData?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(profile);
        }
    }

    private sealed class FakeJobClient(
        JobMatchingData? job,
        Exception? exception = null) : IJobMatchingDataClient
    {
        public Task<JobMatchingData?> GetByIdAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            if (exception is not null)
                return Task.FromException<JobMatchingData?>(exception);

            return Task.FromResult(job);
        }
    }

    private sealed class FakeScoringEngine : IMatchingScoringEngine
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(
            ProfileMatchingData profile,
            JobMatchingData job,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            IReadOnlyCollection<MatchingCriterion> criteria =
            [
                new MatchingCriterion(
                    "JobTitle",
                    80m,
                    1m,
                    "Job title is compatible.")
            ];

            return Task.FromResult(criteria);
        }

    }
    [Fact]
    public async Task HandleAsync_ShouldReturnInsufficientData_WhenCriteriaCannotBeEvaluated()
    {
        var handler = new CalculateMatchingHandler(
            new FakeProfileClient(CreateProfile()),
            new FakeJobClient(CreateJob()),
            new NotEvaluatedScoringEngine());

        var result = await handler.HandleAsync(JobId);

        Assert.NotNull(result);
        Assert.Equal(MatchingStatus.InsufficientData, result.Status);
        Assert.Null(result.OverallScore);
    }

    internal sealed class NotEvaluatedScoringEngine : IMatchingScoringEngine
    {
        public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(
            ProfileMatchingData profile,
            JobMatchingData job,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<MatchingCriterion> criteria =
            [
                MatchingCriterion.NotEvaluated(
                "Skills",
                0.5m,
                "Job requirements have not been extracted."),

            MatchingCriterion.NotEvaluated(
                "Location",
                0.5m,
                "Location preferences are missing.")
            ];

            return Task.FromResult(criteria);
        }
    }
}
