using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.Infrastructure.Repositories;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;

namespace JobApplicationAgent.Job.IntegrationTests.Api;

public sealed class RecommendableJobsTests(JobApiFactory factory)
    : IClassFixture<JobApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/jobs/recommendable";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();

    [Fact]
    public async Task Get_ShouldFilterBeforePagination_AndKeepExistingEndpointUnfiltered()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var jobs = await SeedAsync(db, JobStatus.Discovered, JobStatus.Ignored,
            JobStatus.Saved, JobStatus.Expired, JobStatus.Discovered);
        using var client = factory.CreateClient();

        var first = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=1&pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=2&pageSize=2"))!;
        var all = (await client.GetFromJsonAsync<JsonElement[]>("/api/v1/jobs?pageSize=100"))!;

        Assert.Equal(new[] { jobs[4].Id, jobs[2].Id }, first.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal("Saved", first[1].GetProperty("status").GetString());
        Assert.Equal(jobs[0].Id, Assert.Single(second).GetProperty("id").GetGuid());
        Assert.Equal(5, all.Length);
        Assert.Contains(all, item => item.GetProperty("status").GetString() == "Ignored");
        Assert.Contains(all, item => item.GetProperty("status").GetString() == "Expired");
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=3&pageSize=2"))!);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Get_ShouldUseIdToBreakCreationDateTies_WithoutDuplicates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var jobs = await SeedAsync(db, JobStatus.Saved, JobStatus.Discovered, JobStatus.Saved);
        foreach (var job in jobs)
            db.Entry(job).Property(x => x.CreatedAtUtc).CurrentValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();

        var first = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=1&pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=2&pageSize=2"))!;

        Assert.Equal(jobs.Select(job => job.Id).Order(),
            first.Concat(second).Select(item => item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Get_ShouldExcludeUnknownStoredStatus_WithoutMaterializingIt()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var jobs = await SeedAsync(db, JobStatus.Discovered, JobStatus.Saved);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE job_offers SET status = 'UnknownFutureStatus' WHERE id = {jobs[1].Id}");
        using var client = factory.CreateClient();

        var items = (await client.GetFromJsonAsync<JsonElement[]>(Route))!;

        Assert.Equal(jobs[0].Id, Assert.Single(items).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_ShouldReturnEmptyArray_WhenNoEligibleJobsExist()
    {
        using var client = factory.CreateClient();
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(Route))!);
        using var scope = factory.Services.CreateScope();
        await SeedAsync(scope.ServiceProvider.GetRequiredService<JobDbContext>(), JobStatus.Ignored, JobStatus.Expired);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(Route))!);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=1000001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=-1")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task Get_ShouldReturn400_WhenPaginationIsInvalid(string query)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?" + query)).StatusCode);
    }

    [Fact]
    public async Task Repository_ShouldHandleEmptyStatuses_Duplicates_AndRejectUnknownValues()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        await SeedAsync(db, JobStatus.Saved);
        var repository = new JobRepository(db);

        Assert.Empty(await repository.GetPageByStatusesAsync([], 1, 20));
        Assert.Single(await repository.GetPageByStatusesAsync([JobStatus.Saved, JobStatus.Saved], 1, 20));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetPageByStatusesAsync([(JobStatus)99], 1, 20));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.GetPageByStatusesAsync(null!, 1, 20));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.GetPageByStatusesAsync([JobStatus.Saved], 0, 20));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.GetPageByStatusesAsync([JobStatus.Saved], 1, 101));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetPageByStatusesAsync([JobStatus.Saved], 1, 20, cancellation.Token));
    }

    private static async Task<JobEntity[]> SeedAsync(JobDbContext db, params JobStatus[] statuses)
    {
        var jobs = statuses.Select(status =>
        {
            var job = JobEntity.Create("Engineer", "Company", "Paris", WorkMode.Hybrid,
                ContractType.Permanent, null, null, null, "Description", "Manual", null, null);
            job.ChangeStatus(status);
            return job;
        }).ToArray();
        db.Jobs.AddRange(jobs);
        for (var index = 0; index < jobs.Length; index++)
            db.Entry(jobs[index]).Property(job => job.CreatedAtUtc).CurrentValue =
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index);
        await db.SaveChangesAsync();
        return jobs;
    }
}
