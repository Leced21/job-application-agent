using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Get;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Get;

public sealed class GetJobByIdHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnJob_WhenItExists()
    {
        var repository =
            Substitute.For<IJobRepository>();

        var job = CreateJob();

        repository
            .GetByIdAsync(
                job.Id,
                Arg.Any<CancellationToken>())
            .Returns(job);

        var handler =
            new GetJobByIdHandler(repository);

        var result =
            await handler.HandleAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal(job.Title, result.Title);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var repository =
            Substitute.For<IJobRepository>();

        repository
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns((JobEntity?)null);

        var handler =
            new GetJobByIdHandler(repository);

        var result =
            await handler.HandleAsync(Guid.NewGuid());

        Assert.Null(result);
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