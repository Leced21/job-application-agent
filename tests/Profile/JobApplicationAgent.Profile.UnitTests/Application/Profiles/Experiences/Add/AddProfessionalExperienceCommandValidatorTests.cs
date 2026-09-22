using FluentValidation.TestHelper;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Experiences.Add;

public sealed class AddProfessionalExperienceCommandValidatorTests
{
    private readonly AddProfessionalExperienceCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        var command = new AddProfessionalExperienceCommand(
            CompanyName: "Test Company",
            JobTitle: "Data Engineer",
            Location: "Paris",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: new DateOnly(2025, 1, 1),
            IsCurrent: false,
            Description: "Développement de pipelines de données.");

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldNotHaveErrors_WhenExperienceIsCurrent()
    {
        var command = new AddProfessionalExperienceCommand(
            CompanyName: "Test Company",
            JobTitle: "Data Engineer",
            Location: "Paris",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: null,
            IsCurrent: true,
            Description: "Développement de pipelines de données.");

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCompanyNameIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            CompanyName = string.Empty
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenJobTitleIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            JobTitle = string.Empty
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.JobTitle);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenEndDateIsBeforeStartDate()
    {
        var command = CreateValidCommand() with
        {
            StartDate = new DateOnly(2025, 1, 1),
            EndDate = new DateOnly(2024, 1, 1)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCurrentExperienceHasEndDate()
    {
        var command = CreateValidCommand() with
        {
            IsCurrent = true,
            EndDate = new DateOnly(2025, 1, 1)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.EndDate)
            .WithErrorMessage(
                "EndDate must be null for a current professional experience.");
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCompanyNameExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            CompanyName = new string('A', 201)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenJobTitleExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            JobTitle = new string('A', 151)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.JobTitle);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenLocationExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Location = new string('A', 201)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Location);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenDescriptionExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Description = new string('A', 4001)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    private static AddProfessionalExperienceCommand CreateValidCommand()
    {
        return new AddProfessionalExperienceCommand(
            CompanyName: "Test Company",
            JobTitle: "Data Engineer",
            Location: "Paris",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: new DateOnly(2025, 1, 1),
            IsCurrent: false,
            Description: "Développement de pipelines de données.");
    }
}