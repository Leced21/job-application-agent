using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Tests;

public static class CompositionSamples
{
    public sealed record Sample(ResumeDocument Resume, ResumeTextExtraction Extraction, ResumeCandidateFact[] Facts, PreparedDocumentDraft Draft);
    public static Sample Create(string kind = "Resume", Guid? ownerId = null, bool dates = false)
    {
        var owner = ownerId ?? Guid.NewGuid(); var now = DateTime.UtcNow;
        var resume = ResumeDocument.Create(owner, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), now);
        var specifications = new[] {
            ("Skill", new Dictionary<string,string> { ["Name"] = "SQL" }),
            ("Experience", new Dictionary<string,string> { ["Title"] = "Engineer", ["Company"] = "Acme", ["Period"] = "2020-2024", ["Description"] = "No Azure certification claimed" }),
            ("Education", new Dictionary<string,string> { ["Degree"] = "MSc", ["Institution"] = "University", ["Period"] = "2018-2020" }),
            ("Certification", new Dictionary<string,string> { ["Name"] = "Cloud", ["Issuer"] = "Institute", ["Period"] = "2021", ["Identifier"] = "CERT-1" }) };
        var segments = specifications.Select((spec, index) => new TextSegment(index + 1, string.Join(" | ", spec.Item2.Values))).ToArray();
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", segments)), now);
        var facts = specifications.Select((spec, index) => CandidateFactBuilder.Create(extraction, new(spec.Item1,
            spec.Item2.Select(x => new EvidenceSelection(x.Key, index + 1, segments[index].Text.IndexOf(x.Value, StringComparison.Ordinal), x.Value.Length)).ToArray()), now)).ToArray();
        foreach (var fact in facts) fact.Review(owner, "Approved", "Checked source", now);
        var confirmed = new ConfirmProfileDatesCommand(new(2020, 3, 12), new(2024, 7, 19), false, null, null, true, "Dates checked separately");
        if (dates) facts[1].ConfirmProfileDates(owner, JsonSerializer.Serialize(confirmed), now);
        var snapshots = facts.Select(fact => new DraftFactSnapshot(CandidateFactDto.From(fact),
            fact.ProfileDatesJson is null ? null : confirmed, fact.ProfileDatesConfirmedByOwnerId, fact.ProfileDatesConfirmedAtUtc)).ToArray();
        var source = new DraftSourceSnapshot("prepared-draft-v1", new(Guid.NewGuid(), "Backend developer", "Hiring Co", "Paris", "Rust mandatory", "Manual", null, "Saved", now),
            new(ResumeMetadata.From(resume), null, snapshots));
        var draft = PreparedDocumentDraft.Create(owner, Guid.NewGuid(), resume.Id, source.Job.Id, kind, new string('a', 64), JsonSerializer.Serialize(source), now);
        return new(resume, extraction, facts, draft);
    }
}
