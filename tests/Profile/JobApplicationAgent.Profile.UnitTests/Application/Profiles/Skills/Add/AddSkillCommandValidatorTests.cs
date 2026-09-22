using FluentValidation.TestHelper;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Skills.Add;

public sealed class AddSkillCommandValidatorTests
{
    private readonly AddSkillCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldNotHaveErrors_WhenCommandIsValid()
    {
        var command = CreateValidCommand();

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenNameIsEmpty()
    {
        var command = CreateValidCommand() with
        {
            Name = string.Empty
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenNameExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Name = new string('A', 151)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenCategoryExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Category = new string('A', 101)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenLevelExceedsMaximumLength()
    {
        var command = CreateValidCommand() with
        {
            Level = new string('A', 51)
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Level);
    }

    [Fact]
    public async Task Validate_ShouldHaveError_WhenYearsOfExperienceIsNegative()
    {
        var command = CreateValidCommand() with
        {
            YearsOfExperience = -1
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(
            x => x.YearsOfExperience);
    }

    [Fact]
    public async Task Validate_ShouldNotHaveError_WhenYearsOfExperienceIsZero()
    {
        var command = CreateValidCommand() with
        {
            YearsOfExperience = 0
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveValidationErrorFor(
            x => x.YearsOfExperience);
    }

    [Fact]
    public async Task Validate_ShouldNotHaveErrors_WhenOptionalFieldsAreNull()
    {
        var command = CreateValidCommand() with
        {
            Category = null,
            Level = null,
            YearsOfExperience = null
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static AddSkillCommand CreateValidCommand()
    {
        return new AddSkillCommand(
            Name: "Databricks",
            Category: "Data Engineering",
            Level: "Advanced",
            YearsOfExperience: 4);
    }
}