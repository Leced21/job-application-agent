using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Tests;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class DocumentCompositionTests
{
    [Theory]
    [InlineData("Resume")]
    [InlineData("CoverLetter")]
    public void Composition_ShouldBeDeterministicAndUseOnlyApprovedCandidateValues(string kind)
    {
        var sample = CompositionSamples.Create(kind); var output = VerifiedDocumentComposer.Compose(sample.Draft);
        Assert.Equal(JsonSerializer.Serialize(output), JsonSerializer.Serialize(VerifiedDocumentComposer.Compose(sample.Draft)));
        Assert.Equal(sample.Facts.Select(x => x.Id), output.Blocks.Select(x => x.FactId)); Assert.Equal(4, output.Blocks.Length);
        Assert.Contains("No Azure certification claimed", output.PlainText); Assert.Contains("2020-2024", output.PlainText);
        Assert.DoesNotContain("Rust", output.PlainText); Assert.DoesNotContain("2020-01-01", output.PlainText);
        Assert.Contains("Candidature au poste : Backend developer", output.PlainText);
        Assert.DoesNotContain("passionné", output.PlainText); Assert.Equal(kind == "CoverLetter", output.PlainText.Contains("Madame, Monsieur,"));
        Assert.All(output.Blocks, block => Assert.All(block.Evidence, evidence => Assert.Equal(block.FactId, evidence.FactId)));
        Assert.Contains(output.Notices, x => x.Contains("Identité"));
    }
    [Fact]
    public void ConfirmedDates_ShouldBeLabelledSeparatelyFromVerbatimPeriod()
    {
        var sample = CompositionSamples.Create(dates: true); var output = VerifiedDocumentComposer.Compose(sample.Draft);
        var experience = output.Blocks.Single(x => x.Kind == "Experience");
        Assert.Equal("OwnerConfirmed", experience.DatesOrigin);
        Assert.Contains("Période indiquée dans le CV : 2020-2024", experience.Text);
        Assert.Contains("Dates confirmées par le candidat : 2020-03-12 — 2024-07-19", experience.Text);
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public void DateRendering_ShouldRespectCurrentStatusAndCategory(string kind)
    {
        var sample = CompositionSamples.Create(); var draft = sample.Draft;
        var source = JsonSerializer.Deserialize<DraftSourceSnapshot>(draft.SnapshotJson)!;
        var item = source.Candidate.Facts.Single(x => x.Fact.Kind == kind);
        var dates = kind == "Certification" ? new ConfirmProfileDatesCommand(null, null, null, new(2021, 3, 12), null, true, "Checked")
            : new ConfirmProfileDatesCommand(new(2020, 3, 12), null, true, null, null, true, "Checked");
        source = source with { Candidate = source.Candidate with { Facts = [item with {
            Dates = dates, DatesConfirmedByOwnerId = draft.OwnerId, DatesConfirmedAtUtc = DateTime.UtcNow }] } };
        var changed = PreparedDocumentDraft.Create(draft.OwnerId, Guid.NewGuid(), draft.ResumeId, draft.JobId, draft.Kind,
            draft.RequestHash, JsonSerializer.Serialize(source), DateTime.UtcNow);
        var block = Assert.Single(VerifiedDocumentComposer.Compose(changed).Blocks);
        Assert.Contains(kind == "Certification" ? "émission 2021-03-12" : "2020-03-12 — en cours", block.Text);
        Assert.DoesNotContain("expiration", block.Text);
    }
    [Theory]
    [InlineData("schema")]
    [InlineData("job")]
    [InlineData("decision")]
    [InlineData("owner")]
    [InlineData("resume")]
    [InlineData("sha")]
    [InlineData("kind")]
    [InlineData("fields")]
    [InlineData("duplicate")]
    [InlineData("dates")]
    public void InvalidFrozenSources_ShouldNotBeComposed(string invalid)
    {
        var sample = CompositionSamples.Create(dates: true); var draft = sample.Draft;
        var source = JsonSerializer.Deserialize<DraftSourceSnapshot>(draft.SnapshotJson)!; var item = source.Candidate.Facts[0];
        var fact = item.Fact;
        source = invalid switch {
            "schema" => source with { SchemaVersion = "unknown" }, "job" => source with { Job = source.Job with { Id = Guid.NewGuid() } },
            "duplicate" => source with { Candidate = source.Candidate with { Facts = [item, item] } },
            _ => source with { Candidate = source.Candidate with { Facts = [item with { Fact = invalid switch {
                "decision" => fact with { Decision = "Pending" }, "owner" => fact with { ReviewedByOwnerId = Guid.NewGuid() },
                "resume" => fact with { ResumeId = Guid.NewGuid() }, "sha" => fact with { SourceSha256 = new string('b', 64) },
                "kind" => fact with { Kind = "Unknown" }, "fields" => fact with { Fields = [] }, _ => fact },
                Dates = invalid == "dates" ? source.Candidate.Facts[1].Dates : null }] } } };
        var changed = PreparedDocumentDraft.Create(draft.OwnerId, Guid.NewGuid(), draft.ResumeId, draft.JobId, draft.Kind,
            draft.RequestHash, JsonSerializer.Serialize(source), draft.CreatedAtUtc);
        Assert.Throws<DraftRejectedException>(() => VerifiedDocumentComposer.Compose(changed));
    }
    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void Review_ShouldBeFinalAttributedAndBoundToExactHash(string decision)
    {
        var sample = CompositionSamples.Create(); var draft = sample.Draft;
        var composition = DocumentComposition.Create(draft.OwnerId, draft.Id, draft.Kind, VerifiedDocumentComposer.Version,
            draft.SnapshotSha256, JsonSerializer.Serialize(VerifiedDocumentComposer.Compose(draft)), DateTime.UtcNow);
        var now = DateTime.UtcNow; composition.Review(draft.OwnerId, composition.ContentSha256, decision, "Checked", now);
        var reviewedAt = composition.ReviewedAtUtc;
        composition.Review(draft.OwnerId, composition.ContentSha256, decision, "Checked", now.AddHours(1)); Assert.Equal(reviewedAt, composition.ReviewedAtUtc);
        Assert.Equal(draft.OwnerId, composition.ReviewedByOwnerId);
        Assert.Throws<DraftConflictException>(() => composition.Review(draft.OwnerId, new string('a', 64), decision, "Checked", now));
        Assert.Throws<DraftConflictException>(() => composition.Review(draft.OwnerId, composition.ContentSha256, decision == "Approved" ? "Rejected" : "Approved", "Checked", now));
        Assert.Throws<DraftConflictException>(() => composition.Review(draft.OwnerId, composition.ContentSha256, decision, "Other note", now));
        Assert.Throws<ArgumentException>(() => composition.Review(Guid.NewGuid(), composition.ContentSha256, decision, "Checked", now));
    }
}
