using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Create;

public sealed class CreateJobCommandValidatorTests
{
    private readonly CreateJobCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var command = CreateValidCommand();

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenTitleIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            Title = ""
        };

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.Title));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenCompanyNameIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            CompanyName = ""
        };

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.CompanyName));
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenSalaryMinIsNegative()
    {
        var command = CreateValidCommand() with
        {
            SalaryMin = -1
        };

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenSalaryMaxIsLowerThanSalaryMin()
    {
        var command = CreateValidCommand() with
        {
            SalaryMin = 70_000m,
            SalaryMax = 60_000m
        };

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    private static CreateJobCommand CreateValidCommand()
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
            Description: "Build data platforms.",
            Source: "Manual",
            SourceUrl: "https://example.com/jobs/123",
            PublishedAtUtc: DateTime.UtcNow.AddDays(-1));
    }
}