namespace JobApplicationAgent.Documents.Domain;

public sealed class ResumeTextExtraction
{
    private ResumeTextExtraction() { }
    public Guid Id { get; private set; }
    public Guid ResumeId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string SourceSha256 { get; private set; } = "";
    public string EngineVersion { get; private set; } = "";
    public DateTime ExtractedAtUtc { get; private set; }
    public string ReviewStatus { get; private set; } = "PendingReview";
    public string ResultJson { get; private set; } = "";
    public static ResumeTextExtraction Create(ResumeDocument document, string version, string json, DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("An extraction requires a version, result and UTC date.");
        return new() { Id = Guid.NewGuid(), ResumeId = document.Id, OwnerId = document.OwnerId,
            SourceSha256 = document.Sha256, EngineVersion = version, ResultJson = json,
            ExtractedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
