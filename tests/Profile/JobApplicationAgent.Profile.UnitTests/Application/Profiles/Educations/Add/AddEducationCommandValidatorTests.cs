using FluentValidation.TestHelper;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Educations.Add;

public sealed class AddEducationCommandValidatorTests
{
    private readonly AddEducationCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        var command = CreateValidCommand();

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenInstitutionNameIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            InstitutionName = string.Empty
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.InstitutionName);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenInstitutionNameExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            InstitutionName = new string('A', 201)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.InstitutionName);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenDegreeIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            Degree = string.Empty
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Degree);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenDegreeExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Degree = new string('A', 151)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Degree);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenFieldOfStudyExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            FieldOfStudy = new string('A', 201)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.FieldOfStudy);
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

    [Fact]
    public async Task Validate_ShouldHaveError_WhenEndDateIsBeforeStartDate()
    {
        var command = CreateValidCommand() with
        {
            StartDate = new DateOnly(2022, 9, 1),
            EndDate = new DateOnly(2022, 8, 31),
            IsCurrent = false
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.EndDate);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCurrentEducationHasEndDate()
    {
        var command = CreateValidCommand() with
        {
            IsCurrent = true,
            EndDate = new DateOnly(2026, 6, 30)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.EndDate)
            .WithErrorMessage(
                "EndDate must be null for a current education.");
    }

    [Fact]
    public async Task Validate_ShouldNotHaveEndDateError_WhenCurrentEducationHasNoEndDate()
    {
        var command = CreateValidCommand() with
        {
            IsCurrent = true,
            EndDate = null
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(x => x.EndDate);
    }

    private static AddEducationCommand CreateValidCommand()
    {
        return new AddEducationCommand(
            InstitutionName: "Université Paris-Saclay",
            Degree: "Master",
            FieldOfStudy: "Data Science",
            Location: "Paris",
            StartDate: new DateOnly(2022, 9, 1),
            EndDate: new DateOnly(2024, 6, 30),
            IsCurrent: false,
            Description: "Master spécialisé en Data Science.");
    }
}