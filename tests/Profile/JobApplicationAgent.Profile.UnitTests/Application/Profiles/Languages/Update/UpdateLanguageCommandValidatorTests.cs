using JobApplicationAgent.Profile.Application.Profiles.Languages.Update;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Update;

public sealed class UpdateLanguageCommandValidatorTests
{
    private readonly UpdateLanguageCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new UpdateLanguageCommand(
            "French",
            "Native");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var command = new UpdateLanguageCommand(
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
        var command = new UpdateLanguageCommand(
            new string('a', 101),
            "Native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenProficiencyLevelIsEmpty()
    {
        var command = new UpdateLanguageCommand(
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
        var command = new UpdateLanguageCommand(
            "French",
            new string('a', 51));

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}