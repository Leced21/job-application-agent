using JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Certifications.Add;

public sealed class AddCertificationCommandValidatorTests
{
    private readonly AddCertificationCommandValidator _validator = new();

    [Fact]
    public async Task Validate_ShouldSucceed_WhenCommandIsValid()
    {
        var command = new AddCertificationCommand(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _validator.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameIsEmpty()
    {
        var command = new AddCertificationCommand(
            "",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(command.Name));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenNameExceedsMaximumLength()
    {
        var command = new AddCertificationCommand(
            new string('a', 201),
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIssuingOrganizationIsEmpty()
    {
        var command = new AddCertificationCommand(
            "Azure",
            "",
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.PropertyName ==
                     nameof(command.IssuingOrganization));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIssuingOrganizationExceedsMaximumLength()
    {
        var command = new AddCertificationCommand(
            "Azure",
            new string('a', 201),
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/certificate")]
    [InlineData("http://example.com/certificate")]
    public void Validate_ShouldAcceptOptionalFieldsAndSameDayExpiry(string? url)
    {
        var date = new DateOnly(2025, 1, 1);
        var command = new AddCertificationCommand("Azure", "Microsoft", date, date, null, url);
        Assert.True(_validator.Validate(command).IsValid);
    }

    [Theory]
    [InlineData("issueDate")]
    [InlineData("expirationDate")]
    [InlineData("credentialId")]
    [InlineData("credentialUrl")]
    [InlineData("urlLength")]
    public void Validate_ShouldRejectInvalidCertificationDetails(string field)
    {
        var command = new AddCertificationCommand(
            "Azure", "Microsoft", new DateOnly(2025, 1, 1), null, null, null);
        command = field switch
        {
            "issueDate" => command with { IssueDate = default },
            "expirationDate" => command with { ExpirationDate = new DateOnly(2024, 1, 1) },
            "credentialId" => command with { CredentialId = new string('a', 201) },
            "credentialUrl" => command with { CredentialUrl = "ftp://example.com/file" },
            _ => command with { CredentialUrl = "https://example.com/" + new string('a', 2000) }
        };
        Assert.False(_validator.Validate(command).IsValid);
    }
}