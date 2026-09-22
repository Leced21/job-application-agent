using JobApplicationAgent.Profile.Application.Profiles.Links.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Links.Add;

public sealed class AddLinkCommandValidatorTests
{
    private readonly AddLinkCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new AddLinkCommand(
            "French",
            "https://example.com/native");

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var command = new AddLinkCommand(
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
        var command = new AddLinkCommand(
            new string('a', 101),
            "https://example.com/native");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUrlIsEmpty()
    {
        var command = new AddLinkCommand(
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
        var command = new AddLinkCommand(
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
        var command = new AddLinkCommand("Portfolio", url!);
        Assert.Equal(valid, _validator.Validate(command).IsValid);
    }
}