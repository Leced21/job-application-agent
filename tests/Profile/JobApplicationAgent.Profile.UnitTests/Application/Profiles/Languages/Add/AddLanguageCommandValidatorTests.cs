using JobApplicationAgent.Profile.Application.Profiles.Languages.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Add;

public sealed class AddLanguageCommandValidatorTests
{
    private readonly AddLanguageCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new AddLanguageCommand(
            "French",
            "Native");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var command = new AddLanguageCommand(
            "",
            "Native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.Name));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaximumLength()
    {
        var command = new AddLanguageCommand(
            new string('a', 101),
            "Native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenProficiencyLevelIsEmpty()
    {
        var command = new AddLanguageCommand(
            "French",
            "");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                     nameof(command.ProficiencyLevel));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenProficiencyLevelExceedsMaximumLength()
    {
        var command = new AddLanguageCommand(
            "French",
            new string('a', 51));

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}