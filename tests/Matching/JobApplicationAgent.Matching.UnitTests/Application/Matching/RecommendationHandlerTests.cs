using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class RecommendationHandlerTests
{
    private static readonly Guid CandidateId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ShouldRankJobsByDescendingScore()
    {
        var profileClient = new FakeProfileClient(CreateProfile());
        var jobClient = new FakeJobClient(
        [
            CreateJob("Job A"),
            CreateJob("Job B"),
            CreateJob("Job C")
        ]);

        var scoringEngine = new FakeScoringEngine(
            new Dictionary<string, decimal?>
            {
                ["Job A"] = 60m,
                ["Job B"] = 90m,
                ["Job C"] = 75m
            });

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            scoringEngine,
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Job B", result.Items[0].Title);
        Assert.Equal("Job C", result.Items[1].Title);
        Assert.Equal("Job A", result.Items[2].Title);
    }

    [Fact]
    public async Task HandleAsync_ShouldFetchProfileOnlyOnce()
    {
        var profileClient = new FakeProfileClient(CreateProfile());
        var jobClient = new FakeJobClient(
        [
            CreateJob("Job A"),
            CreateJob("Job B")
        ]);

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            new FakeScoringEngine(),
            new RecommendationRankingService());

        await handler.HandleAsync();

        Assert.Equal(1, profileClient.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenProfileDoesNotExist()
    {
        var profileClient = new FakeProfileClient(null);
        var jobClient = new FakeJobClient([CreateJob("Job A")]);

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            new FakeScoringEngine(),
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.Null(result);
        Assert.Equal(0, jobClient.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyRecommendations_WhenNoJobsExist()
    {
        var handler = new RecommendationHandler(
            new FakeProfileClient(CreateProfile()),
            new FakeJobClient([]),
            new FakeScoringEngine(),
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(CandidateId, result.CandidateProfileId);
    }

    [Fact]
    public async Task HandleAsync_ShouldPutUnscoredJobsLast()
    {
        var profileClient = new FakeProfileClient(CreateProfile());
        var jobClient = new FakeJobClient(
        [
            CreateJob("Unscored Job"),
            CreateJob("Scored Job")
        ]);

        var scoringEngine = new FakeScoringEngine(
            new Dictionary<string, decimal?>
            {
                ["Unscored Job"] = null,
                ["Scored Job"] = 80m
            });

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            scoringEngine,
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.NotNull(result);
        Assert.Equal("Scored Job", result.Items[0].Title);
        Assert.Equal("Unscored Job", result.Items[1].Title);
        Assert.Null(result.Items[1].OverallScore);
        Assert.Equal("InsufficientData", result.Items[1].Status);
    }


    [Fact]
    public async Task HandleAsync_ShouldRankByRankingScore()
    {
        var profileClient = new FakeProfileClient(CreateProfile());

        var jobClient = new FakeJobClient(
        [
            CreateJob("Datacorp"),
        CreateJob("DataTech Solutions")
        ]);

        var scoringEngine = new FakeScoringEngine(
            new Dictionary<string, decimal?>
            {
                ["Datacorp"] = 100m,
                ["DataTech Solutions"] = 90m
            });

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            scoringEngine,
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.NotNull(result);

        // Les deux offres ont ici une couverture de 100 %,
        // car le FakeScoringEngine renvoie un critère évalué.
        Assert.Equal("Datacorp", result.Items[0].Title);
        Assert.Equal(100m, result.Items[0].RankingScore);
        Assert.Equal(90m, result.Items[1].RankingScore);
    }

    [Fact]
    public async Task HandleAsync_ShouldPreferHigherCoverage_WhenRankingScoreIsHigher()
    {
        var profileClient = new FakeProfileClient(CreateProfile());

        var jobClient = new FakeJobClient(
        [
            CreateJob("Datacorp"),
        CreateJob("DataTech Solutions")
        ]);

        var scoringEngine = new FakeScoringEngine(
            scores: new Dictionary<string, decimal?>
            {
                ["Datacorp"] = 100m,
                ["DataTech Solutions"] = 90m
            },
            coverages: new Dictionary<string, decimal>
            {
                ["Datacorp"] = 50m,
                ["DataTech Solutions"] = 100m
            });

        var handler = new RecommendationHandler(
            profileClient,
            jobClient,
            scoringEngine,
            new RecommendationRankingService());

        var result = await handler.HandleAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);

        var first = result.Items[0];
        var second = result.Items[1];

        Assert.Equal("DataTech Solutions", first.Title);
        Assert.Equal(90m, first.OverallScore);
        Assert.Equal(100m, first.EvaluationCoverage);
        Assert.Equal(90m, first.RankingScore);

        Assert.Equal("Datacorp", second.Title);
        Assert.Equal(100m, second.OverallScore);
        Assert.Equal(50m, second.EvaluationCoverage);
        Assert.Equal(50m, second.RankingScore);
    }


    [Theory]
    [InlineData(0, 20)]
    [InlineData(1_000_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task HandleAsync_ShouldRejectInvalidPagination(
        int page,
        int pageSize)
    {
        var handler = new RecommendationHandler(
            new FakeProfileClient(CreateProfile()),
            new FakeJobClient([]),
            new FakeScoringEngine(),
            new RecommendationRankingService());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(page, pageSize));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(101)]
    public async Task HandleAsync_ShouldRankEntireCatalogueBeforePagination(int jobCount)
    {
        var jobs = Enumerable.Range(1, jobCount)
            .Select(index => CreateJob($"Job {index}"))
            .ToArray();
        var scores = jobs.Select((job, index) => (job.Title, Score: (decimal?)index))
            .ToDictionary(item => item.Title, item => item.Score);
        var jobClient = new FakeJobClient(jobs);
        var handler = new RecommendationHandler(
            new FakeProfileClient(CreateProfile()), jobClient,
            new FakeScoringEngine(scores), new RecommendationRankingService());

        var first = await handler.HandleAsync(page: 1, pageSize: 1);
        var second = await handler.HandleAsync(page: 2, pageSize: 1);
        var beyondEnd = await handler.HandleAsync(page: jobCount + 1, pageSize: 1);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(beyondEnd);
        Assert.Equal(jobs[^1].Id, Assert.Single(first.Items).JobId);
        Assert.Equal(jobs[^2].Id, Assert.Single(second.Items).JobId);
        Assert.Empty(beyondEnd.Items);
        Assert.Equal(6, jobClient.CallCount);
    }

    private static ProfileMatchingData CreateProfile() =>
        new(
            CandidateId,
            "Data Engineer",
            "Experienced Data Engineer",
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
            "Hybrid",
            "Permanent",
            50000m,
            65000m,
            "EUR",
            "Python and SQL");

    private sealed class FakeProfileClient(ProfileMatchingData? profile) : IProfileMatchingDataClient
    {
        public int CallCount { get; private set; }

        public Task<ProfileMatchingData?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(profile);
        }
    }

    private sealed class FakeJobClient(IReadOnlyList<JobMatchingData> jobs) : IJobMatchingDataClient
    {
        public int CallCount { get; private set; }

        public Task<JobMatchingData?> GetByIdAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                jobs.FirstOrDefault(job => job.Id == jobId));
        }

        public Task<IReadOnlyList<JobMatchingData>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Recommendations must use eligible jobs.");

        public Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult<IReadOnlyList<JobMatchingData>>(
                jobs.Skip((page - 1) * pageSize).Take(pageSize).ToArray());
        }
    }


    private sealed class FakeScoringEngine(
        Dictionary<string, decimal?>? scores = null,
        Dictionary<string, decimal>? coverages = null)
        : IMatchingScoringEngine
    {
        public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(
            ProfileMatchingData profile,
            JobMatchingData job,
            CancellationToken cancellationToken = default)
        {
            decimal? score =
                scores is not null &&
                scores.TryGetValue(job.Title, out var configuredScore)
                    ? configuredScore
                    : 80m;

            decimal coverage =
                coverages is not null &&
                coverages.TryGetValue(job.Title, out var configuredCoverage)
                    ? configuredCoverage
                    : 100m;

            IReadOnlyCollection<MatchingCriterion> criteria;

            if (!score.HasValue)
            {
                criteria =
                [
                    MatchingCriterion.NotEvaluated(
                    "JobTitle",
                    1m,
                    "Insufficient data")
                ];
            }
            else if (coverage == 50m)
            {
                criteria =
                [
                    new MatchingCriterion(
                    "JobTitle",
                    score.Value,
                    0.5m,
                    "Test score"),

                MatchingCriterion.NotEvaluated(
                    "Skills",
                    0.5m,
                    "Requirements unavailable")
                ];
            }
            else
            {
                criteria =
                [
                    new MatchingCriterion(
                    "JobTitle",
                    score.Value,
                    1m,
                    "Test score")
                ];
            }
            return Task.FromResult(criteria);
        }
    }

}
