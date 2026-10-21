using FluentValidation;
using JobApplicationAgent.Profile.Application.Profiles.DocumentFacts;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.DocumentFacts;

public sealed class ImportDocumentFactHandlerTests
{
    private static ImportDocumentFactCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new string('a', 64), "resume-text-v1", "Skill", "Approved", DateTime.UtcNow, Guid.Empty,
        [new("Name", "SQL", 1, 0, 3, "SQL")]);
    [Theory]
    [InlineData("fact-id")]
    [InlineData("resume-id")]
    [InlineData("extraction-id")]
    [InlineData("decision")]
    [InlineData("reviewer")]
    [InlineData("review-date")]
    [InlineData("sha")]
    [InlineData("engine")]
    [InlineData("kind")]
    [InlineData("fields")]
    [InlineData("name")]
    [InlineData("length")]
    [InlineData("range")]
    [InlineData("excerpt")]
    public async Task InvalidImport_ShouldNotReachPersistence(string invalid)
    {
        var command = Valid(); command = command with { ReviewedByOwnerId = command.OwnerId };
        var field = command.Fields[0];
        command = invalid switch
        {
            "fact-id" => command with { FactId = Guid.Empty },
            "resume-id" => command with { ResumeId = Guid.Empty },
            "extraction-id" => command with { ExtractionId = Guid.Empty },
            "decision" => command with { Decision = "Pending" },
            "reviewer" => command with { ReviewedByOwnerId = Guid.NewGuid() },
            "review-date" => command with { ReviewedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) },
            "sha" => command with { SourceSha256 = "invalid" },
            "engine" => command with { EngineVersion = "" },
            "kind" => command with { Kind = "Experience" },
            "fields" => command with { Fields = [] },
            "name" => command with { Fields = [field with { Value = new string('a', 151), Length = 151 }] },
            "length" => command with { Fields = [field with { Length = 2 }] },
            "range" => command with { Fields = [field with { Start = -1 }] },
            _ => command with { Fields = [field with { Excerpt = "Unrelated evidence" }] }
        };
        var repository = Substitute.For<IDocumentFactImportRepository>();
        await Assert.ThrowsAsync<ValidationException>(() => new ImportDocumentFactHandler(repository)
            .HandleAsync(command.OwnerId, command.TargetProfileId, command, default));
        Assert.Empty(repository.ReceivedCalls());
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrongBinding_ShouldNotReachPersistence(bool wrongOwner)
    {
        var command = Valid(); command = command with { ReviewedByOwnerId = command.OwnerId };
        var repository = Substitute.For<IDocumentFactImportRepository>();
        await Assert.ThrowsAsync<DocumentFactImportConflictException>(() => new ImportDocumentFactHandler(repository).HandleAsync(
            wrongOwner ? Guid.NewGuid() : command.OwnerId, wrongOwner ? command.TargetProfileId : Guid.NewGuid(), command, default));
        Assert.Empty(repository.ReceivedCalls());
    }
    private static ImportDocumentFactCommand Dated(string kind)
    {
        var command = Valid(); var now = DateTime.UtcNow;
        var labels = kind switch
        {
            "Experience" => new Dictionary<string, string> { ["Title"] = "Engineer", ["Company"] = "Acme", ["Period"] = "2020-2024" },
            "Education" => new() { ["Degree"] = "MSc", ["Institution"] = "University", ["Period"] = "2020-2024" },
            _ => new() { ["Name"] = "Cloud", ["Issuer"] = "Institute", ["Period"] = "2021-2024" }
        };
        return command with { Kind = kind, ReviewedByOwnerId = command.OwnerId, ReviewedAtUtc = now.AddMinutes(-1),
            Fields = labels.Select(x => new ImportedEvidenceField(x.Key, x.Value, 1, 0, x.Value.Length, x.Value)).ToArray(),
            Dates = kind == "Certification"
                ? new(null, null, null, new(2021, 3, 12), new(2024, 7, 19), true, "Verified", command.OwnerId, now)
                : new(new(2020, 3, 12), new(2024, 7, 19), false, null, null, true, "Verified", command.OwnerId, now) };
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("owner")]
    [InlineData("confirmation")]
    [InlineData("date-audit")]
    [InlineData("before-review")]
    [InlineData("note")]
    [InlineData("long-note")]
    [InlineData("start")]
    [InlineData("minimum")]
    [InlineData("end")]
    [InlineData("order")]
    [InlineData("current")]
    [InlineData("status")]
    [InlineData("cross-category")]
    [InlineData("label")]
    [InlineData("duplicate")]
    public async Task InvalidDatedImport_ShouldNotReachPersistence(string invalid)
    {
        var command = Dated("Experience"); var dates = command.Dates!;
        command = invalid switch
        {
            "missing" => command with { Dates = null },
            "owner" => command with { Dates = dates with { ConfirmedByOwnerId = Guid.NewGuid() } },
            "confirmation" => command with { Dates = dates with { Confirmed = false } },
            "date-audit" => command with { Dates = dates with { ConfirmedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) } },
            "before-review" => command with { Dates = dates with { ConfirmedAtUtc = command.ReviewedAtUtc.AddMinutes(-1) } },
            "note" => command with { Dates = dates with { ConfirmationNote = " " } },
            "long-note" => command with { Dates = dates with { ConfirmationNote = new string('a', 1001) } },
            "start" => command with { Dates = dates with { StartDate = null } },
            "minimum" => command with { Dates = dates with { StartDate = DateOnly.MinValue } },
            "end" => command with { Dates = dates with { EndDate = null } },
            "order" => command with { Dates = dates with { EndDate = new(2019, 1, 1) } },
            "current" => command with { Dates = dates with { IsCurrent = true } },
            "status" => command with { Dates = dates with { IsCurrent = null } },
            "cross-category" => command with { Dates = dates with { IssueDate = new(2021, 1, 1) } },
            "label" => command with { Fields = command.Fields.Where(x => x.Field != "Company").ToArray() },
            _ => command with { Fields = [.. command.Fields, command.Fields[0]] }
        };
        var repository = Substitute.For<IDocumentFactImportRepository>();
        await Assert.ThrowsAsync<ValidationException>(() => new ImportDocumentFactHandler(repository)
            .HandleAsync(command.OwnerId, command.TargetProfileId, command, default));
        Assert.Empty(repository.ReceivedCalls());
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public async Task SupportedDatedImport_ShouldPersistValidatedProvenance(string kind)
    {
        var command = Dated(kind); var repository = Substitute.For<IDocumentFactImportRepository>();
        var receipt = new DocumentFactReceipt(command.FactId, command.OwnerId, command.TargetProfileId, Guid.Empty,
            command.SourceSha256, "Created", DateTime.UtcNow, kind, Guid.NewGuid());
        repository.ImportAsync(command, Arg.Any<string>(), Arg.Any<string>(), default).Returns(new DocumentFactImportOutcome(receipt, true));
        var result = await new ImportDocumentFactHandler(repository).HandleAsync(command.OwnerId, command.TargetProfileId, command, default);
        Assert.Equal(receipt, result.Receipt);
        await repository.Received(1).ImportAsync(command, Arg.Is<string>(x => x.Length == 64), Arg.Is<string>(x => x.Contains("ConfirmedByOwnerId")), default);
    }
    [Theory]
    [InlineData("issue")]
    [InlineData("expiration")]
    [InlineData("employment")]
    public async Task InvalidCertification_ShouldNotReachPersistence(string invalid)
    {
        var command = Dated("Certification"); var dates = command.Dates!;
        command = command with { Dates = invalid switch
        {
            "issue" => dates with { IssueDate = null }, "expiration" => dates with { ExpirationDate = new(2020, 1, 1) },
            _ => dates with { IsCurrent = false }
        } };
        var repository = Substitute.For<IDocumentFactImportRepository>();
        await Assert.ThrowsAsync<ValidationException>(() => new ImportDocumentFactHandler(repository).HandleAsync(command.OwnerId, command.TargetProfileId, command, default));
        Assert.Empty(repository.ReceivedCalls());
    }
}
