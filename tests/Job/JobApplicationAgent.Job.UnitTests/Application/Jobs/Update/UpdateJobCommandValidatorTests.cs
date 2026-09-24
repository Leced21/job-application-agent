using JobApplicationAgent.Job.Application.Jobs.Update;
using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.UnitTests.Application.Jobs.Update;

public sealed class UpdateJobCommandValidatorTests
{
    private readonly UpdateJobCommandValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var command = CreateValidCommand();

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ShouldFail_WhenSalaryMaxIsLowerThanSalaryMin()
    {
        var command = CreateValidCommand() with
        {
            SalaryMin = 80_000m,
            SalaryMax = 60_000m
        };

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    private static UpdateJobCommand CreateValidCommand()
    {
        return new UpdateJobCommand(
            Title: "Senior Data Engineer",
            CompanyName: "Test Company",
            Location: "Paris",
            WorkMode: WorkMode.Hybrid,
            ContractType: ContractType.Permanent,
            SalaryMin: 60_000m,
            SalaryMax: 75_000m,
            SalaryCurrency: "EUR",
            Description: "Description",
            Source: "Manual",
            SourceUrl: null,
            PublishedAtUtc: null);
    }
}