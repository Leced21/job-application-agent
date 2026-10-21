using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record TextSegment(int Index, string Text);
public sealed record ExtractedText(string EngineVersion, string UnitKind, TextSegment[] Segments)
{
    public const string Version = "resume-text-v1-pdfpig-0.1.16";
    public const int MaxCharacters = 200000;
    public void Validate()
    {
        if (EngineVersion != Version || UnitKind is not ("Page" or "Paragraph") || Segments is null ||
            Segments.Length < 1 || Segments.Length > (UnitKind == "Page" ? 40 : 2000) ||
            Segments.Where((segment, index) => segment is null || segment.Index != index + 1 || segment.Text is null).Any() ||
            Segments.Sum(x => (long)x.Text.Length) > MaxCharacters)
            throw new TextExtractionRejectedException("Invalid or oversized extraction result.");
        if (!Segments.Any(x => !string.IsNullOrWhiteSpace(x.Text)))
            throw new TextExtractionRejectedException("No extractable text; OCR is not available in this stage.");
    }
}
public sealed class TextExtractionRejectedException(string message) : Exception(message);
public sealed class TextExtractionUnavailableException(string message) : Exception(message);
public interface IResumeTextExtractor
{
    Task<ExtractedText> ExtractAsync(ResumeDocument document, CancellationToken ct);
}
public interface IResumeExtractionRepository
{
    Task<ResumeTextExtraction?> GetAsync(Guid ownerId, Guid resumeId, string version, CancellationToken ct);
    Task<ResumeTextExtraction> SaveOrGetAsync(ResumeTextExtraction extraction, CancellationToken ct);
}
public sealed record ResumeExtractionDto(Guid Id, Guid ResumeId, string SourceSha256, string EngineVersion,
    DateTime ExtractedAtUtc, string ReviewStatus, string UnitKind, TextSegment[] Segments)
{
    public static ResumeExtractionDto From(ResumeTextExtraction extraction)
    {
        var result = System.Text.Json.JsonSerializer.Deserialize<ExtractedText>(extraction.ResultJson)!;
        return new(extraction.Id, extraction.ResumeId, extraction.SourceSha256, extraction.EngineVersion,
            extraction.ExtractedAtUtc, extraction.ReviewStatus, result.UnitKind, result.Segments);
    }
}
public sealed class ResumeExtractionService(IResumeRepository resumes, IResumeExtractionRepository repository,
    IResumeTextExtractor extractor, TimeProvider clock)
{
    public async Task<ResumeExtractionDto?> ExtractAsync(Guid ownerId, Guid resumeId, CancellationToken ct)
    {
        var document = await resumes.GetAsync(ownerId, resumeId, ct);
        if (document is null) return null;
        var saved = await repository.GetAsync(ownerId, resumeId, ExtractedText.Version, ct);
        if (saved is not null) return ResumeExtractionDto.From(saved);
        var result = await extractor.ExtractAsync(document, ct);
        result.Validate();
        var extraction = ResumeTextExtraction.Create(document, result.EngineVersion,
            System.Text.Json.JsonSerializer.Serialize(result), clock.GetUtcNow().UtcDateTime);
        return ResumeExtractionDto.From(await repository.SaveOrGetAsync(extraction, ct));
    }
    public async Task<ResumeExtractionDto?> GetAsync(Guid ownerId, Guid resumeId, CancellationToken ct)
    {
        var extraction = await repository.GetAsync(ownerId, resumeId, ExtractedText.Version, ct);
        return extraction is null ? null : ResumeExtractionDto.From(extraction);
    }
}
