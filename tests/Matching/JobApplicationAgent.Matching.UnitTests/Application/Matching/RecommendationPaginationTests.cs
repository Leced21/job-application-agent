using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class RecommendationPaginationTests
{
    [Theory]
    [InlineData(0, 1, 20, 0, 0)]
    [InlineData(20, 1, 20, 20, 1)]
    [InlineData(21, 2, 20, 1, 2)]
    [InlineData(21, 3, 20, 0, 2)]
    [InlineData(100, 1, 100, 100, 1)]
    [InlineData(101, 2, 100, 1, 2)]
    [InlineData(101, 1_000_000, 100, 0, 2)]
    public async Task HandleAsync_ShouldReturnExactTotals(int count, int page, int pageSize, int items, int pages)
    {
        var result = await CreateHandler(new BatchClient(Jobs(count))).HandleAsync(page, pageSize);
        Assert.NotNull(result);
        Assert.Equal(count, result.TotalCount);
        Assert.Equal(pages, result.TotalPages);
        Assert.Equal(items, result.Items.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(17)]
    public async Task HandleAsync_ShouldMatchFullGlobalSort_ForMixedScoresAndDeepPages(int page)
    {
        var jobs = Jobs(257);
        // Independent full-sort oracle: production retains only a bounded heap.
        var expected = jobs.Select((job, index) => new
        {
            job.Id,
            Score = index % 11 == 0 ? (decimal?)null : index * 37 % 101,
            Coverage = index % 11 == 0 ? 0m : index % 2 == 0 ? 100m : 50m
        }).OrderByDescending(item => item.Score * item.Coverage / 100m)
            .ThenByDescending(item => item.Coverage).ThenBy(item => item.Id)
            .Skip((page - 1) * 17).Take(17).Select(item => item.Id).ToArray();

        var result = await CreateHandler(new BatchClient(jobs), new MixedScoring()).HandleAsync(page, 17);

        Assert.Equal(expected, result!.Items.Select(item => item.JobId));
        Assert.Equal(257, result.TotalCount);
        Assert.Equal(16, result.TotalPages);
    }

    [Fact]
    public async Task HandleAsync_ShouldProcessEachBatchBeforeFetchingNext_AndScoreDuplicatesOnlyOnce()
    {
        var unique = Jobs(101);
        var scorer = new MixedScoring();
        var source = new BatchClient(unique.Take(100).Concat(new[] { unique[0] with { Title = "Job999" }, unique[100] }).ToArray());
        source.BeforeFetch = page =>
        {
            if (page == 2) Assert.Equal(100, scorer.JobIds.Count);
        };

        var result = await CreateHandler(source, scorer).HandleAsync(1, 100);

        Assert.Equal(101, result!.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(101, scorer.JobIds.Count);
        Assert.Equal(101, scorer.JobIds.Distinct().Count());
        Assert.DoesNotContain(result.Items, item => item.Title == "Job999");
    }

    [Fact]
    public async Task HandleAsync_ShouldDeduplicateBeforeCountingAndPagination()
    {
        var jobs = Jobs(2);
        var handler = CreateHandler(new BatchClient([jobs[0], jobs[0], jobs[1], jobs[1]]),
            limits: new RecommendationProcessingLimits { MaxUniqueJobs = 2 });
        var first = await handler.HandleAsync(1, 1);
        var second = await handler.HandleAsync(2, 1);
        Assert.Equal(2, first!.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.NotEqual(Assert.Single(first.Items).JobId, Assert.Single(second!.Items).JobId);
    }

    [Fact]
    public async Task HandleAsync_ShouldRefuseOversizedCatalogue_WithoutReturningPartialResults()
    {
        var handler = CreateHandler(new BatchClient(Jobs(3)),
            limits: new RecommendationProcessingLimits { MaxUniqueJobs = 2 });
        await Assert.ThrowsAsync<RecommendationCapacityExceededException>(() => handler.HandleAsync());
    }

    [Fact]
    public async Task HandleAsync_ShouldBoundRepeatedFullSourcePages()
    {
        var jobs = Jobs(100);
        var source = new BatchClient(jobs) { RepeatFirstBatch = true };
        var scorer = new MixedScoring();
        var handler = CreateHandler(source, scorer,
            new RecommendationProcessingLimits { MaxSourcePages = 2 });

        await Assert.ThrowsAsync<RecommendationCapacityExceededException>(() => handler.HandleAsync());

        Assert.Equal(new[] { 1, 2 }, source.Pages);
        Assert.Equal(100, scorer.JobIds.Count);
    }

    [Fact]
    public async Task HandleAsync_ShouldAcceptExactJobLimit_AfterCheckingTerminalPage()
    {
        var source = new BatchClient(Jobs(100));
        var result = await CreateHandler(source, limits: new RecommendationProcessingLimits
        {
            MaxUniqueJobs = 100, MaxSourcePages = 2
        }).HandleAsync();
        Assert.Equal(100, result!.TotalCount);
        Assert.Equal(new[] { 1, 2 }, source.Pages);
    }

    [Fact]
    public async Task HandleAsync_ShouldNotReusePriorProfileOrCatalogue()
    {
        var profile = Substitute.For<IProfileMatchingDataClient>();
        var firstProfile = Profile();
        var nextProfile = Profile();
        profile.GetAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult<ProfileMatchingData?>(firstProfile), Task.FromResult<ProfileMatchingData?>(nextProfile));
        var source = new BatchClient(Jobs(2));
        var handler = new RecommendationHandler(profile, source, new MixedScoring(), new RecommendationRankingService());
        var first = await handler.HandleAsync();
        source.Jobs = [];
        var next = await handler.HandleAsync();
        Assert.Equal(firstProfile.CandidateProfileId, first!.CandidateProfileId);
        Assert.Equal(nextProfile.CandidateProfileId, next!.CandidateProfileId);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(0, next.TotalCount);
        Assert.Empty(next.Items);
    }

    [Fact]
    public async Task HandleAsync_ShouldStopWhenCallerCancelsDuringScoring()
    {
        using var cancellation = new CancellationTokenSource();
        var scorer = Substitute.For<IMatchingScoringEngine>();
        scorer.EvaluateAsync(Arg.Any<ProfileMatchingData>(), Arg.Any<JobMatchingData>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromResult<IReadOnlyCollection<MatchingCriterion>>([new("Title", 80m, 1m, "Test")]);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateHandler(new BatchClient(Jobs(2)), scorer).HandleAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task HandleAsync_ShouldNotReturnMissingProfile_AfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var profile = Substitute.For<IProfileMatchingDataClient>();
        profile.GetAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult<ProfileMatchingData?>(null);
        });
        var handler = new RecommendationHandler(profile, new BatchClient([]), new MixedScoring(), new RecommendationRankingService());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task HandleAsync_ShouldEnforceOverallDeadline()
    {
        var profile = Substitute.For<IProfileMatchingDataClient>();
        profile.GetAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return (ProfileMatchingData?)null;
        });
        var handler = new RecommendationHandler(profile, new BatchClient([]), new MixedScoring(),
            new RecommendationRankingService(), new RecommendationProcessingLimits { TimeoutSeconds = 1 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync());
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(100_001, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1_000_001, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 301)]
    public void ProcessingLimits_ShouldRejectUnsafeConfiguration(int jobs, int pages, int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecommendationProcessingLimits
        {
            MaxUniqueJobs = jobs, MaxSourcePages = pages, TimeoutSeconds = seconds
        }.Validate());
    }

    private static RecommendationHandler CreateHandler(BatchClient client,
        IMatchingScoringEngine? scorer = null, RecommendationProcessingLimits? limits = null)
    {
        var profile = Substitute.For<IProfileMatchingDataClient>();
        profile.GetAsync(Arg.Any<CancellationToken>()).Returns(Profile());
        return new RecommendationHandler(profile, client, scorer ?? new MixedScoring(), new RecommendationRankingService(), limits);
    }

    private static ProfileMatchingData Profile() => new(Guid.NewGuid(), "Engineer", null, [], [], [], [], [], null);
    private static JobMatchingData[] Jobs(int count) => Enumerable.Range(0, count)
        .Select(index => new JobMatchingData(Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"),
            $"Job{index}", "Company", null, "Remote", "Permanent", null, null, null, null)).ToArray();

    private sealed class BatchClient(IReadOnlyList<JobMatchingData> jobs) : IJobMatchingDataClient
    {
        public IReadOnlyList<JobMatchingData> Jobs { get; set; } = jobs;
        public List<int> Pages { get; } = [];
        public Action<int>? BeforeFetch { get; set; }
        public bool RepeatFirstBatch { get; init; }
        public Task<JobMatchingData?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<JobMatchingData>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            Pages.Add(page);
            BeforeFetch?.Invoke(page);
            return Task.FromResult<IReadOnlyList<JobMatchingData>>(Jobs.Skip(RepeatFirstBatch ? 0 : (page - 1) * pageSize).Take(pageSize).ToArray());
        }
    }

    private sealed class MixedScoring : IMatchingScoringEngine
    {
        public List<Guid> JobIds { get; } = [];
        public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(ProfileMatchingData profile,
            JobMatchingData job, CancellationToken cancellationToken = default)
        {
            JobIds.Add(job.Id);
            var index = int.Parse(job.Title.AsSpan(3));
            IReadOnlyCollection<MatchingCriterion> criteria = index % 11 == 0
                ? [MatchingCriterion.NotEvaluated("Title", 1m, "Missing")]
                : index % 2 == 0
                    ? [new("Title", index * 37 % 101, 1m, "Test")]
                    : [new("Title", index * 37 % 101, .5m, "Test"), MatchingCriterion.NotEvaluated("Skills", .5m, "Missing")];
            return Task.FromResult(criteria);
        }
    }
}
