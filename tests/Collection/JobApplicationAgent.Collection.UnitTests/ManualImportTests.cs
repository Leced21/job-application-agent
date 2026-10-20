using FluentValidation;
using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class ManualImportTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static ImportOfferCommand Command() => new("  Data   Engineer ", " Example   Company ", " Paris ",
        "télétravail", "CDI", 50000m, 60000m, "eur", "Python and Spark", ["pyspark", "Spark", "sql"], "Manual");
    private static CollectedOffer Normalize(ImportOfferCommand command) => new OfferNormalizer().Normalize(command, Now);

    [Fact]
    public void Normalize_ShouldPreserveOrigin_AndNormalizeProvidedFields()
    {
        var offer = Normalize(Command());
        Assert.Equal("  Data   Engineer ", offer.OriginalTitle);
        Assert.Equal("Data Engineer", offer.Title);
        Assert.Equal("Example Company", offer.CompanyName);
        Assert.Equal("Paris", offer.Location);
        Assert.Equal("Remote", offer.WorkMode);
        Assert.Equal("Permanent", offer.ContractType);
        Assert.Equal("EUR", offer.SalaryCurrency);
        Assert.Equal(new[] { "Apache Spark", "SQL" }, offer.Skills);
        Assert.Equal(Now, offer.FirstCollectedAtUtc);
        Assert.Equal(1, offer.SeenCount);
        Assert.Equal("manual-v1", offer.NormalizationVersion);
    }

    [Fact]
    public void Normalize_ShouldNotInferSkillsOrMissingFields()
    {
        var offer = Normalize(new ImportOfferCommand("Engineer", "Company", Description: "Python is mentioned here"));
        Assert.Empty(offer.Skills);
        Assert.Equal("Unknown", offer.WorkMode);
        Assert.Equal("Unknown", offer.ContractType);
        Assert.Null(offer.SourceUrl);
        Assert.Null(offer.SalaryMin);
        Assert.Equal("Manual", offer.SourceName);
    }

    [Theory]
    [InlineData("https://EXAMPLE.com:443/jobs/42?utm_source=test&id=1#section", "https://example.com/jobs/42?id=1")]
    [InlineData("http://example.com:80/job?gclid=tracking", "http://example.com/job")]
    [InlineData("https://example.com/job?ref=company&x=2", "https://example.com/job?ref=company&x=2")]
    public void Normalize_ShouldCanonicalizeUrlWithoutRemovingMeaningfulQuery(string source, string expected)
    {
        var offer = Normalize(Command() with { SourceUrl = source });
        Assert.Equal(expected, offer.SourceUrl);
    }

    [Fact]
    public void Normalize_ShouldPreserveAdzunaAttributionParameters()
    {
        var offer = Normalize(Command() with
        {
            SourceName = "Adzuna",
            SourceUrl = "https://www.adzuna.fr/jobs/land/ad/123?utm_source=app&id=123"
        });

        Assert.Equal("https://www.adzuna.fr/jobs/land/ad/123?utm_source=app&id=123", offer.SourceUrl);
    }

    [Fact]
    public void Deduplication_ShouldUseCanonicalUrl_AndDetectChangedContentSeparately()
    {
        var first = Normalize(Command() with { SourceUrl = "https://example.com/job?utm_source=a" });
        var next = Normalize(Command() with { SourceUrl = "https://EXAMPLE.com:443/job#apply", Title = "Changed title" });
        Assert.Equal(first.DeduplicationKey, next.DeduplicationKey);
        Assert.NotEqual(first.ContentFingerprint, next.ContentFingerprint);
        Assert.NotEqual(first.Id, next.Id);
    }

    [Fact]
    public void Deduplication_ShouldNormalizeEquivalentContent_AndSeparateDistinctUrls()
    {
        var first = Normalize(Command());
        var next = Normalize(Command() with { Title = "data engineer", CompanyName = "EXAMPLE COMPANY", Skills = ["SQL", "Apache Spark"] });
        Assert.Equal(first.DeduplicationKey, next.DeduplicationKey);
        Assert.NotEqual(Normalize(Command() with { SourceUrl = "https://example.com/1" }).DeduplicationKey,
            Normalize(Command() with { SourceUrl = "https://example.com/2" }).DeduplicationKey);
    }

    [Fact]
    public void Fingerprint_ShouldIgnoreInsignificantDecimalScale()
    {
        Assert.Equal(Normalize(Command() with { SalaryMin = 50000m }).ContentFingerprint,
            Normalize(Command() with { SalaryMin = 50000.00m }).ContentFingerprint);
    }

    [Fact]
    public void Expiry_ShouldUseExplicitDate_AndNotInventMissingExpiration()
    {
        Assert.False(Normalize(Command()).IsExpired(Now));
        Assert.True(Normalize(Command() with { ExpiresAtUtc = Now }).IsExpired(Now));
        Assert.False(Normalize(Command() with { ExpiresAtUtc = Now.AddDays(1) }).IsExpired(Now));
    }

    [Theory]
    [InlineData("CDI", "Permanent")]
    [InlineData("CDD", "FixedTerm")]
    [InlineData("stage", "Internship")]
    [InlineData("alternance", "Apprenticeship")]
    [InlineData("intérim", "Temporary")]
    public void Normalize_ShouldMapKnownContractAliases(string input, string expected) =>
        Assert.Equal(expected, Normalize(Command() with { ContractType = input }).ContractType);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/private")]
    [InlineData("https://user:password@example.com/job")]
    [InlineData("not a url")]
    public void Validator_ShouldRejectUnsafeOrInvalidSourceUrl(string url) =>
        Assert.False(new ImportOfferCommandValidator().Validate(Command() with { SourceUrl = url }).IsValid);

    [Theory]
    [InlineData("title")]
    [InlineData("company")]
    [InlineData("mode")]
    [InlineData("contract")]
    [InlineData("salary")]
    [InlineData("currency")]
    [InlineData("publication")]
    [InlineData("expiry")]
    [InlineData("skills")]
    public void Validator_ShouldRejectInvalidFields(string field)
    {
        var command = field switch
        {
            "title" => Command() with { Title = " " },
            "company" => Command() with { CompanyName = new string('x', 201) },
            "mode" => Command() with { WorkMode = "unrecognized" },
            "contract" => Command() with { ContractType = "unrecognized" },
            "salary" => Command() with { SalaryMin = 70000m },
            "currency" => Command() with { SalaryCurrency = null },
            "publication" => Command() with { PublishedAtUtc = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified) },
            "expiry" => Command() with { PublishedAtUtc = Now, ExpiresAtUtc = Now.AddDays(-1) },
            _ => Command() with { Skills = [null!] }
        };
        Assert.False(new ImportOfferCommandValidator().Validate(command).IsValid);
    }

    [Fact]
    public async Task Handler_ShouldValidateBeforePersistence_AndUseInjectedClock()
    {
        var repository = Substitute.For<ICollectedOfferRepository>();
        var handler = new ImportOfferHandler(new ImportOfferCommandValidator(), new OfferNormalizer(), repository, new Clock());
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(Command() with { Title = "" }));
        await repository.DidNotReceive().ImportAsync(Arg.Any<CollectedOffer>(), Arg.Any<CancellationToken>());
        using var cancellation = new CancellationTokenSource();
        await handler.HandleAsync(Command(), cancellation.Token);
        await repository.Received(1).ImportAsync(Arg.Is<CollectedOffer>(offer => offer.FirstCollectedAtUtc == Now), cancellation.Token);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
