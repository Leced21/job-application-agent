namespace JobApplicationAgent.Collection.Domain.Entities;

public sealed record OfferContent(
    string Title, string CompanyName, string? Location, string WorkMode, string ContractType,
    decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, string? Description,
    string[] Skills, string SourceName, string? SourceUrl, DateTime? PublishedAtUtc, DateTime? ExpiresAtUtc);

public sealed class CollectedOffer
{
    private CollectedOffer() { }
    public Guid Id { get; private set; }
    public string OriginalTitle { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string CompanyName { get; private set; } = "";
    public string? Location { get; private set; }
    public string WorkMode { get; private set; } = "Unknown";
    public string ContractType { get; private set; } = "Unknown";
    public decimal? SalaryMin { get; private set; }
    public decimal? SalaryMax { get; private set; }
    public string? SalaryCurrency { get; private set; }
    public string? Description { get; private set; }
    public string[] Skills { get; private set; } = [];
    public string SourceName { get; private set; } = "Manual";
    public string? SourceUrl { get; private set; }
    public string DeduplicationKey { get; private set; } = "";
    public string ContentFingerprint { get; private set; } = "";
    public string NormalizationVersion { get; private set; } = "manual-v1";
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime FirstCollectedAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public long SeenCount { get; private set; }
    public bool HasPendingChanges { get; private set; }

    public bool IsExpired(DateTime now) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= now;

    public static CollectedOffer Create(OfferContent content, string originalTitle,
        string deduplicationKey, string contentFingerprint, DateTime collectedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content.Title) || string.IsNullOrWhiteSpace(content.CompanyName))
            throw new ArgumentException("Title and company are required.", nameof(content));
        if (deduplicationKey?.Length != 64 || contentFingerprint?.Length != 64)
            throw new ArgumentException("A SHA256 identity is required.");
        if (collectedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Collection date must be UTC.", nameof(collectedAtUtc));

        return new CollectedOffer
        {
            Id = Guid.NewGuid(), OriginalTitle = originalTitle, Title = content.Title,
            CompanyName = content.CompanyName, Location = content.Location, WorkMode = content.WorkMode,
            ContractType = content.ContractType, SalaryMin = content.SalaryMin, SalaryMax = content.SalaryMax,
            SalaryCurrency = content.SalaryCurrency, Description = content.Description, Skills = content.Skills.ToArray(),
            SourceName = content.SourceName, SourceUrl = content.SourceUrl, PublishedAtUtc = content.PublishedAtUtc,
            ExpiresAtUtc = content.ExpiresAtUtc, DeduplicationKey = deduplicationKey, ContentFingerprint = contentFingerprint,
            FirstCollectedAtUtc = collectedAtUtc, LastSeenAtUtc = collectedAtUtc, SeenCount = 1
        };
    }
}
