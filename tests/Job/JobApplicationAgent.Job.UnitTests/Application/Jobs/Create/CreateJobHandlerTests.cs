using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using NSubstitute;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Create;

public sealed class CreateJobHandlerTests
{
    private readonly IJobRepository _repository;
    private readonly CreateJobHandler _handler;

    public CreateJobHandlerTests()
    {
        _repository = Substitute.For<IJobRepository>();
        _handler = new CreateJobHandler(_repository, new CreateJobCommandValidator());
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateAndPersistJob()
    {
        var command = CreateCommand();

        var result = await _handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(command.Title, result.Title);
        Assert.Equal(command.CompanyName, result.CompanyName);
        Assert.Equal(command.Location, result.Location);
        Assert.Equal(command.WorkMode, result.WorkMode);
        Assert.Equal(command.ContractType, result.ContractType);
        Assert.Equal(command.SalaryMin, result.SalaryMin);
        Assert.Equal(command.SalaryMax, result.SalaryMax);
        Assert.Equal(command.SalaryCurrency, result.SalaryCurrency);
        Assert.Equal(command.Description, result.Description);
        Assert.Equal(command.Source, result.Source);
        Assert.Equal(command.SourceUrl, result.SourceUrl);
        Assert.Equal(command.PublishedAtUtc, result.PublishedAtUtc);

        Assert.Equal(
            JobStatus.Discovered,
            result.Status);

        await _repository.Received(1)
            .AddAsync(
                Arg.Is<JobEntity>(job =>
                    job.Id == result.Id &&
                    job.Title == command.Title &&
                    job.CompanyName == command.CompanyName),
                Arg.Any<CancellationToken>());

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    private static CreateJobCommand CreateCommand()
    {
        return new CreateJobCommand(
            Title: "Data Engineer",
            CompanyName: "Test Company",
            Location: "Paris",
            WorkMode: WorkMode.Hybrid,
            ContractType: ContractType.Permanent,
            SalaryMin: 55_000m,
            SalaryMax: 70_000m,
            SalaryCurrency: "EUR",
            Description: "Build and maintain data platforms.",
            Source: "Manual",
            SourceUrl: "https://example.com/jobs/123",
            PublishedAtUtc: DateTime.UtcNow.AddDays(-1));
    }
}