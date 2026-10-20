using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record EvidenceSelection(string Field, int SegmentIndex, int Start, int Length);
public sealed record CreateCandidateFactCommand(string Kind, EvidenceSelection?[]? Fields);
public sealed record ReviewCandidateFactCommand(string Decision, string? Note = null);
public sealed record CandidateFactField(string Field, string Value, int SegmentIndex, int Start, int Length, string Excerpt);
public sealed record CandidateFactSchema(string Kind, string RequiredField, string[] AllowedFields);
public sealed record CandidateFactDto(Guid Id, Guid ResumeId, Guid ExtractionId, string SourceSha256, string EngineVersion,
    string Kind, string UnitKind, CandidateFactField[] Fields, string Decision, DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc, Guid? ReviewedByOwnerId, string? ReviewNote)
{
    public static CandidateFactDto From(ResumeCandidateFact fact) => new(fact.Id, fact.ResumeId, fact.ExtractionId,
        fact.SourceSha256, fact.EngineVersion, fact.Kind, fact.UnitKind, JsonSerializer.Deserialize<CandidateFactField[]>(fact.FieldsJson)!,
        fact.Decision, fact.CreatedAtUtc, fact.ReviewedAtUtc, fact.ReviewedByOwnerId, fact.ReviewNote);
}
public sealed record CandidateFactCreation(CandidateFactDto Fact, bool Created);
public interface IResumeCandidateFactRepository
{
    Task<(ResumeCandidateFact Fact, bool Created)> SaveOrGetAsync(ResumeCandidateFact fact, CancellationToken ct);
    Task<ResumeCandidateFact?> GetAsync(Guid ownerId, Guid resumeId, Guid factId, CancellationToken ct);
    Task<IReadOnlyList<ResumeCandidateFact>> GetPageAsync(Guid ownerId, Guid resumeId, string? decision, int page, int pageSize, CancellationToken ct);
    Task<ResumeCandidateFact?> ReviewAsync(Guid ownerId, Guid resumeId, Guid factId, string decision, string? note, DateTime now, CancellationToken ct);
}
public static class CandidateFactBuilder
{
    public static IReadOnlyList<CandidateFactSchema> Schemas { get; } = Array.AsReadOnly(new[]
    {
        new CandidateFactSchema("Skill", "Name", ["Name"]),
        new CandidateFactSchema("Experience", "Title", ["Title", "Company", "Period", "Description"]),
        new CandidateFactSchema("Education", "Degree", ["Degree", "Institution", "Period", "Description"]),
        new CandidateFactSchema("Certification", "Name", ["Name", "Issuer", "Period", "Identifier"])
    });
    public static ResumeCandidateFact Create(ResumeTextExtraction extraction, CreateCandidateFactCommand command, DateTime now)
    {
        var schema = Schemas.SingleOrDefault(x => x.Kind == command.Kind);
        if (schema is null || command.Fields is null || command.Fields.Length < 1 || command.Fields.Length > schema.AllowedFields.Length ||
            command.Fields.Any(x => x is null || !schema.AllowedFields.Contains(x.Field)) ||
            command.Fields.Select(x => x!.Field).Distinct(StringComparer.Ordinal).Count() != command.Fields.Length ||
            !command.Fields.Any(x => x!.Field == schema.RequiredField))
            throw new InvalidResumeException("Unknown fact kind, missing required field, unsupported or duplicate fields.");
        var source = JsonSerializer.Deserialize<ExtractedText>(extraction.ResultJson)!;
        var fields = new List<CandidateFactField>();
        foreach (var selection in command.Fields.OrderBy(x => x!.Field, StringComparer.Ordinal))
        {
            var segment = source.Segments.SingleOrDefault(x => x.Index == selection!.SegmentIndex);
            if (segment is null || selection!.Start < 0 || selection.Length is < 1 or > 1000 ||
                selection.Start > segment.Text.Length || selection.Length > segment.Text.Length - selection.Start)
                throw new InvalidResumeException("Evidence must select 1–1000 characters within an existing source segment.");
            var end = selection.Start + selection.Length;
            if (SplitsSurrogate(segment.Text, selection.Start) || SplitsSurrogate(segment.Text, end))
                throw new InvalidResumeException("Evidence boundaries must not split a Unicode character.");
            var value = segment.Text.Substring(selection.Start, selection.Length);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidResumeException("Evidence cannot be whitespace only.");
            var contextStart = Math.Max(0, selection.Start - 120);
            var contextEnd = Math.Min(segment.Text.Length, end + 120);
            if (SplitsSurrogate(segment.Text, contextStart)) contextStart--;
            if (SplitsSurrogate(segment.Text, contextEnd)) contextEnd++;
            fields.Add(new(selection.Field, value, selection.SegmentIndex, selection.Start, selection.Length,
                segment.Text[contextStart..contextEnd]));
        }
        var canonical = JsonSerializer.Serialize(new { extraction.Id, command.Kind,
            Fields = fields.Select(x => new { x.Field, x.SegmentIndex, x.Start, x.Length }) });
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return ResumeCandidateFact.Create(extraction, command.Kind, source.UnitKind, fingerprint, JsonSerializer.Serialize(fields), now);
    }
    private static bool SplitsSurrogate(string text, int index) => index > 0 && index < text.Length &&
        char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);
}
public sealed class ResumeCandidateFactService(IResumeExtractionRepository extractions, IResumeCandidateFactRepository facts, TimeProvider clock)
{
    public async Task<CandidateFactCreation?> CreateAsync(Guid ownerId, Guid resumeId, CreateCandidateFactCommand command, CancellationToken ct)
    {
        var extraction = await extractions.GetAsync(ownerId, resumeId, ExtractedText.Version, ct);
        if (extraction is null) return null;
        var fact = CandidateFactBuilder.Create(extraction, command, clock.GetUtcNow().UtcDateTime);
        var saved = await facts.SaveOrGetAsync(fact, ct);
        return new(CandidateFactDto.From(saved.Fact), saved.Created);
    }
    public async Task<CandidateFactDto?> GetAsync(Guid ownerId, Guid resumeId, Guid factId, CancellationToken ct)
    {
        var fact = await facts.GetAsync(ownerId, resumeId, factId, ct);
        return fact is null ? null : CandidateFactDto.From(fact);
    }
    public async Task<IReadOnlyList<CandidateFactDto>?> GetPageAsync(Guid ownerId, Guid resumeId, string? decision, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100 || decision is not (null or "Pending" or "Approved" or "Rejected"))
            throw new InvalidResumeException("Invalid fact pagination or decision filter.");
        if (await extractions.GetAsync(ownerId, resumeId, ExtractedText.Version, ct) is null) return null;
        return (await facts.GetPageAsync(ownerId, resumeId, decision, page, pageSize, ct)).Select(CandidateFactDto.From).ToArray();
    }
    public async Task<CandidateFactDto?> ReviewAsync(Guid ownerId, Guid resumeId, Guid factId, ReviewCandidateFactCommand command, CancellationToken ct)
    {
        if (command.Decision is not ("Approved" or "Rejected") || command.Note?.Length > 1000)
            throw new InvalidResumeException("Decision must be Approved or Rejected; note is at most 1000 characters.");
        var fact = await facts.ReviewAsync(ownerId, resumeId, factId, command.Decision, command.Note,
            clock.GetUtcNow().UtcDateTime, ct);
        return fact is null ? null : CandidateFactDto.From(fact);
    }
}
