using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using Microsoft.Extensions.DependencyInjection;
using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;

namespace JobApplicationAgent.Job.IntegrationTests.Api;

// Consumer/producer contract: real Job API + PostgreSQL, real Matching HTTP client.
public sealed class MatchingJobContractTests(JobApiFactory factory)
    : IClassFixture<JobApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();

    [Fact]
    public async Task Client_ShouldReadProducerContract_AndKeepIndividualLookupForExcludedJobs()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var jobs = await SeedAsync(db, [JobStatus.Discovered, JobStatus.Saved, JobStatus.Ignored, JobStatus.Expired]);
        using var http = factory.CreateClient();
        var client = new JobMatchingHttpClient(http);

        var eligible = await client.GetRecommendablePageAsync(1, 100);
        var unfiltered = await client.GetPageAsync(1, 100);

        Assert.Equal(jobs.Take(2).Select(job => job.Id).Order(), eligible.Select(job => job.Id).Order());
        Assert.Equal(4, unfiltered.Count);
        foreach (var job in eligible)
        {
            Assert.Equal("Data Engineer", job.Title);
            Assert.Equal("Contract Company", job.CompanyName);
            Assert.Equal("Paris", job.Location);
            Assert.Equal("Hybrid", job.WorkMode);
            Assert.Equal("Permanent", job.ContractType);
            Assert.Equal(50000.25m, job.SalaryMin);
            Assert.Equal(65000m, job.SalaryMax);
            Assert.Equal("EUR", job.SalaryCurrency);
            Assert.Equal("Python and SQL", job.Description);
        }
        foreach (var excluded in jobs.Skip(2))
        {
            var individual = await client.GetByIdAsync(excluded.Id);
            Assert.NotNull(individual);
            Assert.Equal(excluded.Id, individual.Id);
        }
        Assert.Empty(await client.GetRecommendablePageAsync(2, 100));
    }

    [Fact]
    public async Task Recommendations_ShouldReadMultipleEligibleBatches_ThenPaginateGlobally()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var statuses = Enumerable.Repeat(JobStatus.Discovered, 100)
            .Concat(new[] { JobStatus.Ignored, JobStatus.Expired, JobStatus.Saved, JobStatus.Saved }).ToArray();
        var jobs = await SeedAsync(db, statuses);
        using var http = factory.CreateClient();
        var handler = new RecommendationHandler(
            new ProfileClient(), new JobMatchingHttpClient(http),
            new StructuredMatchingScoringEngine(new JobTitleSimilarityService(),
                new SkillMatchingService(), new LocationMatchingService()),
            new RecommendationRankingService());

        var first = await handler.HandleAsync(page: 1, pageSize: 100);
        var second = await handler.HandleAsync(page: 2, pageSize: 100);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(100, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(102, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(first.TotalCount, second.TotalCount);
        var items = first.Items.Concat(second.Items).ToArray();
        Assert.Equal(jobs.Where(job => job.Status is JobStatus.Discovered or JobStatus.Saved)
            .Select(job => job.Id).Order(), items.Select(job => job.JobId));
        Assert.Equal(102, items.Select(job => job.JobId).Distinct().Count());
        Assert.All(items, item => Assert.True(item.RankingScore > 0m));

        foreach (var job in jobs)
            job.ChangeStatus(JobStatus.Expired);
        await db.SaveChangesAsync();
        var expired = await handler.HandleAsync();
        Assert.Empty(expired!.Items);
        Assert.Equal(0, expired.TotalCount);
        Assert.Equal(0, expired.TotalPages);
    }

    private static async Task<JobEntity[]> SeedAsync(JobDbContext db, JobStatus[] statuses)
    {
        var jobs = statuses.Select(status =>
        {
            var job = JobEntity.Create("Data Engineer", "Contract Company", "Paris", WorkMode.Hybrid,
                ContractType.Permanent, 50000.25m, 65000m, "EUR", "Python and SQL", "Manual", null, null);
            job.ChangeStatus(status);
            return job;
        }).ToArray();
        db.Jobs.AddRange(jobs);
        await db.SaveChangesAsync();
        return jobs;
    }

    private sealed class ProfileClient : IProfileMatchingDataClient
    {
        private readonly ProfileMatchingData _profile =
            new(Guid.NewGuid(), "Data Engineer", null, [], [], [], [], [], null);
        public Task<ProfileMatchingData?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ProfileMatchingData?>(_profile);
    }
}
