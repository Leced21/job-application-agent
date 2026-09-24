using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Get;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Get;

public sealed class GetJobsHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnJobs()
    {
        var repository =
            Substitute.For<IJobRepository>();

        var job = CreateJob();

        repository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<JobEntity> { job });

        var handler =
            new GetJobsHandler(repository);

        var result = await handler.HandleAsync();

        var returnedJob = Assert.Single(result);

        Assert.Equal(job.Id, returnedJob.Id);
        Assert.Equal(job.Title, returnedJob.Title);
        Assert.Equal(
            job.CompanyName,
            returnedJob.CompanyName);

        await repository.Received(1)
            .GetAllAsync(
                Arg.Any<CancellationToken>());
    }

    private static JobEntity CreateJob()
    {
        return JobEntity.Create(
            "Data Engineer",
            "Test Company",
            "Paris",
            WorkMode.Hybrid,
            ContractType.Permanent,
            55_000m,
            70_000m,
            "EUR",
            "Description",
            "Manual",
            null,
            null);
    }
}