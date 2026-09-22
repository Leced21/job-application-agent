using JobApplicationAgent.Profile.Application.Profiles.Links.Update;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Links.Update;

public sealed class UpdateLinkCommandValidatorTests
{
    private readonly UpdateLinkCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new UpdateLinkCommand(
            "French",
            "https://example.com/native");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var command = new UpdateLinkCommand(
            "",
            "https://example.com/native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.Name));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaximumLength()
    {
        var command = new UpdateLinkCommand(
            new string('a', 101),
            "https://example.com/native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUrlIsEmpty()
    {
        var command = new UpdateLinkCommand(
            "French",
            "");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                     nameof(command.Url));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUrlExceedsMaximumLength()
    {
        var command = new UpdateLinkCommand(
            "French",
            new string('a', 2001));

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com/path", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/relative", false)]
    [InlineData(null, false)]
    public void Validate_ShouldRequireAbsoluteHttpUrl(string? url, bool valid)
    {
        var command = new UpdateLinkCommand("Portfolio", url!);
        Assert.Equal(valid, _validator.Validate(command).IsValid);
    }
}