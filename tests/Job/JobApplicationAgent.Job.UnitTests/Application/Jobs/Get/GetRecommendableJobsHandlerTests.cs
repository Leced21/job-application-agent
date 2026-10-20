using FluentValidation;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Get;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;
using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Get;

public sealed class GetRecommendableJobsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldRequestOnlyEligibleStatuses_AndMapJobs()
    {
        var repository = Substitute.For<IJobRepository>();
        using var cancellation = new CancellationTokenSource();
        var job = JobEntity.Create("Engineer", "Company", "Paris", WorkMode.Hybrid,
            ContractType.Permanent, 50000m, 60000m, "EUR", "Description", "Manual", null, null);
        job.ChangeStatus(JobStatus.Saved);
        repository.GetPageByStatusesAsync(Arg.Any<IReadOnlyCollection<JobStatus>>(), 2, 10, cancellation.Token)
            .Returns(new[] { job });

        var result = await new GetRecommendableJobsHandler(repository).HandleAsync(2, 10, cancellation.Token);

        var item = Assert.Single(result);
        Assert.Equal(job.Id, item.Id);
        Assert.Equal(job.Title, item.Title);
        Assert.Equal(job.CompanyName, item.CompanyName);
        Assert.Equal(JobStatus.Saved, item.Status);
        await repository.Received(1).GetPageByStatusesAsync(
            Arg.Is<IReadOnlyCollection<JobStatus>>(statuses =>
                statuses.Count == 2 && statuses.Contains(JobStatus.Discovered) && statuses.Contains(JobStatus.Saved)),
            2, 10, cancellation.Token);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1_000_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task HandleAsync_ShouldRejectInvalidPagination_BeforeQuery(int page, int pageSize)
    {
        var repository = Substitute.For<IJobRepository>();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new GetRecommendableJobsHandler(repository).HandleAsync(page, pageSize));
        await repository.DidNotReceive().GetPageByStatusesAsync(
            Arg.Any<IReadOnlyCollection<JobStatus>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1_000_000, 100)]
    public async Task HandleAsync_ShouldAcceptBounds_AndReturnEmptyList(int page, int pageSize)
    {
        var repository = Substitute.For<IJobRepository>();
        repository.GetPageByStatusesAsync(Arg.Any<IReadOnlyCollection<JobStatus>>(), page, pageSize,
            Arg.Any<CancellationToken>()).Returns(Array.Empty<JobEntity>());
        Assert.Empty(await new GetRecommendableJobsHandler(repository).HandleAsync(page, pageSize));
    }
}
