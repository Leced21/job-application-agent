using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Delete;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Delete;

public sealed class DeleteJobHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldDeleteJob_WhenItExists()
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
            new DeleteJobHandler(repository);

        var result = await handler.HandleAsync(
            job.Id);

        Assert.True(result);

        repository.Received(1)
            .Delete(job);

        await repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFalse_WhenJobDoesNotExist()
    {
        var repository =
            Substitute.For<IJobRepository>();

        repository
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns((JobEntity?)null);

        var handler =
            new DeleteJobHandler(repository);

        var result = await handler.HandleAsync(
            Guid.NewGuid());

        Assert.False(result);

        repository.DidNotReceive()
            .Delete(Arg.Any<JobEntity>());

        await repository.DidNotReceive()
            .SaveChangesAsync(
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