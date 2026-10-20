using System.Text.Json;

namespace JobApplicationAgent.Collection.Domain.Entities;

public sealed class OfferRevision
{
    private OfferRevision() { }
    public Guid Id { get; private set; }
    public Guid OfferId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string OriginalTitle { get; private set; } = "";
    public string ContentJson { get; private set; } = "";
    public string NormalizationVersion { get; private set; } = "";
    public DateTime CollectedAtUtc { get; private set; }
    public string Decision { get; private set; } = "Pending";
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByOwnerId { get; private set; }
    public OfferContent ReadContent() => JsonSerializer.Deserialize<OfferContent>(ContentJson)!;

    public static OfferRevision From(CollectedOffer offer, Guid offerId) => new()
    {
        Id = Guid.NewGuid(), OfferId = offerId, Fingerprint = offer.ContentFingerprint,
        OriginalTitle = offer.OriginalTitle, NormalizationVersion = offer.NormalizationVersion,
        CollectedAtUtc = offer.FirstCollectedAtUtc,
        ContentJson = JsonSerializer.Serialize(new OfferContent(offer.Title, offer.CompanyName, offer.Location,
            offer.WorkMode, offer.ContractType, offer.SalaryMin, offer.SalaryMax, offer.SalaryCurrency,
            offer.Description, offer.Skills, offer.SourceName, offer.SourceUrl, offer.PublishedAtUtc, offer.ExpiresAtUtc))
    };

    public void Review(string decision, DateTime now, Guid reviewerOwnerId)
    {
        if (decision is not ("Approved" or "Rejected")) throw new ArgumentException("Invalid decision.", nameof(decision));
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("Review date must be UTC.", nameof(now));
        if (reviewerOwnerId == Guid.Empty) throw new ArgumentException("Reviewer owner ID is required.", nameof(reviewerOwnerId));
        if (Decision == decision) return;
        if (Decision != "Pending") throw new RevisionConflictException("A revision decision is final.");
        if (decision == "Approved" && ReadContent().ExpiresAtUtc is { } expiry && expiry <= now)
            throw new RevisionConflictException("An expired revision cannot be approved.");
        Decision = decision;
        ReviewedAtUtc = now;
        ReviewedByOwnerId = reviewerOwnerId;
    }
}

public sealed class RevisionConflictException(string message) : Exception(message);
