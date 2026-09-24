using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Update;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Update;

public sealed class UpdateJobHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldUpdateJob_WhenItExists()
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
            new UpdateJobHandler(repository);

        var command = new UpdateJobCommand(
            Title: "Senior Data Engineer",
            CompanyName: "Updated Company",
            Location: "Lyon",
            WorkMode: WorkMode.Remote,
            ContractType: ContractType.Freelance,
            SalaryMin: 65_000m,
            SalaryMax: 80_000m,
            SalaryCurrency: "EUR",
            Description: "Updated description",
            Source: "CompanyWebsite",
            SourceUrl: "https://example.com/jobs/456",
            PublishedAtUtc: DateTime.UtcNow);

        var result = await handler.HandleAsync(
            job.Id,
            command);

        Assert.NotNull(result);

        Assert.Equal(
            "Senior Data Engineer",
            result.Title);

        Assert.Equal(
            "Updated Company",
            result.CompanyName);

        Assert.Equal("Lyon", result.Location);
        Assert.Equal(WorkMode.Remote, result.WorkMode);

        Assert.Equal(
            ContractType.Freelance,
            result.ContractType);

        Assert.Equal(65_000m, result.SalaryMin);
        Assert.Equal(80_000m, result.SalaryMax);

        await repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
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
            new UpdateJobHandler(repository);

        var command = new UpdateJobCommand(
            "Data Engineer",
            "Test Company",
            null,
            WorkMode.Unknown,
            ContractType.Unknown,
            null,
            null,
            null,
            null,
            "Manual",
            null,
            null);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            command);

        Assert.Null(result);

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