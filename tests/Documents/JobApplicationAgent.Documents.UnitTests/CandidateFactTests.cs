using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Tests;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class CandidateFactTests
{
    private const string Text = "SQL Engineer Acme 2020-2024. Not certified in Azure. MSc University. 😀Data";
    private static ResumeTextExtraction Source()
    {
        var document = ResumeDocument.Create(Guid.NewGuid(), "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        return ResumeTextExtraction.Create(document, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, Text)])), DateTime.UtcNow);
    }
    private static EvidenceSelection Select(string field, string value) => new(field, 1, Text.IndexOf(value, StringComparison.Ordinal), value.Length);
    [Theory]
    [InlineData("Skill", "Name", "SQL")]
    [InlineData("Experience", "Title", "Engineer")]
    [InlineData("Education", "Degree", "MSc")]
    [InlineData("Certification", "Name", "Azure")]
    public void Annotation_ShouldDeriveValueVerbatimAndRemainUnverified(string kind, string field, string value)
    {
        var fact = CandidateFactBuilder.Create(Source(), new(kind, [Select(field, value)]), DateTime.UtcNow);
        var dto = CandidateFactDto.From(fact);
        Assert.Equal(value, Assert.Single(dto.Fields).Value);
        Assert.Equal("Pending", dto.Decision);
        Assert.Null(dto.ReviewedAtUtc);
        if (value == "Azure") Assert.Contains("Not certified in Azure", dto.Fields[0].Excerpt);
    }
    [Theory]
    [InlineData("kind")]
    [InlineData("field")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("negative")]
    [InlineData("overflow")]
    [InlineData("segment")]
    [InlineData("space")]
    [InlineData("surrogate")]
    public void InvalidSelection_ShouldBeRejected(string invalid)
    {
        var selected = Select("Name", "SQL");
        var command = invalid switch
        {
            "kind" => new CreateCandidateFactCommand("Unknown", [selected]),
            "field" => new("Skill", [selected with { Field = "YearsExperience" }]),
            "missing" => new("Skill", []),
            "duplicate" => new("Experience", [Select("Title", "Engineer"), Select("Title", "Engineer")]),
            "negative" => new("Skill", [selected with { Start = -1 }]),
            "overflow" => new("Skill", [selected with { Start = int.MaxValue }]),
            "segment" => new("Skill", [selected with { SegmentIndex = 2 }]),
            "space" => new("Skill", [selected with { Start = 3, Length = 1 }]),
            _ => new("Skill", [selected with { Start = Text.IndexOf("😀", StringComparison.Ordinal) + 1, Length = 1 }])
        };
        Assert.Throws<InvalidResumeException>(() => CandidateFactBuilder.Create(Source(), command, DateTime.UtcNow));
    }
    [Fact]
    public void FieldOrder_ShouldNotChangeTheFactIdentity()
    {
        var extraction = Source();
        var title = Select("Title", "Engineer");
        var company = Select("Company", "Acme");
        var first = CandidateFactBuilder.Create(extraction, new("Experience", [title, company]), DateTime.UtcNow);
        var second = CandidateFactBuilder.Create(extraction, new("Experience", [company, title]), DateTime.UtcNow);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.FieldsJson, second.FieldsJson);
    }
    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void Review_ShouldBeFinalIdempotentAndAttributedToOwner(string decision)
    {
        var fact = CandidateFactBuilder.Create(Source(), new("Skill", [Select("Name", "SQL")]), DateTime.UtcNow);
        var now = DateTime.UtcNow;
        fact.Review(fact.OwnerId, decision, "Reviewed with source", now);
        var date = fact.ReviewedAtUtc;
        fact.Review(fact.OwnerId, decision, "Reviewed with source", now.AddHours(1));
        Assert.Equal(date, fact.ReviewedAtUtc);
        Assert.Equal(fact.OwnerId, fact.ReviewedByOwnerId);
        Assert.Throws<FactReviewConflictException>(() => fact.Review(fact.OwnerId, decision, "Changed note", now));
        Assert.Throws<FactReviewConflictException>(() => fact.Review(fact.OwnerId, decision == "Approved" ? "Rejected" : "Approved", "Reviewed with source", now));
    }
    [Fact]
    public void Review_ShouldRejectAnotherOwner()
    {
        var fact = CandidateFactBuilder.Create(Source(), new("Skill", [Select("Name", "SQL")]), DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => fact.Review(Guid.NewGuid(), "Approved", null, DateTime.UtcNow));
        Assert.Equal("Pending", fact.Decision);
    }
    [Fact]
    public void ConfirmedDates_ShouldBeImmutableAndIdempotentAcrossJsonFormatting()
    {
        var fact = CandidateFactBuilder.Create(Source(), new("Experience", [Select("Title", "Engineer")]), DateTime.UtcNow);
        fact.Review(fact.OwnerId, "Approved", null, DateTime.UtcNow);
        var now = DateTime.UtcNow;
        fact.ConfirmProfileDates(fact.OwnerId, "{\"StartDate\":\"2020-03-12\",\"Confirmed\":true}", now);
        var confirmedAt = fact.ProfileDatesConfirmedAtUtc;
        fact.ConfirmProfileDates(fact.OwnerId, "{ \"Confirmed\": true, \"StartDate\": \"2020-03-12\" }", now.AddHours(1));
        Assert.Equal(confirmedAt, fact.ProfileDatesConfirmedAtUtc);
        Assert.Equal(fact.OwnerId, fact.ProfileDatesConfirmedByOwnerId);
        Assert.Throws<FactReviewConflictException>(() => fact.ConfirmProfileDates(fact.OwnerId, "{\"StartDate\":\"2020-01-01\"}", now));
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("pending")]
    [InlineData("skill")]
    public void DateConfirmation_ShouldRequireApprovedDatedFactAndItsOwner(string invalid)
    {
        var fact = invalid == "skill"
            ? CandidateFactBuilder.Create(Source(), new("Skill", [Select("Name", "SQL")]), DateTime.UtcNow)
            : CandidateFactBuilder.Create(Source(), new("Experience", [Select("Title", "Engineer")]), DateTime.UtcNow);
        if (invalid != "pending") fact.Review(fact.OwnerId, "Approved", null, DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => fact.ConfirmProfileDates(invalid == "owner" ? Guid.NewGuid() : fact.OwnerId, "{}", DateTime.UtcNow));
        Assert.Null(fact.ProfileDatesJson);
    }
}
