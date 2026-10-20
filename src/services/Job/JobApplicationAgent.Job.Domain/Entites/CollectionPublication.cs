namespace JobApplicationAgent.Job.Domain.Entities;

// Deliberately no cascading FK: deleting a Job must not allow a retry to recreate it.
public sealed class CollectionPublication
{
    private CollectionPublication() { }
    public Guid OfferId { get; private set; }
    public Guid RevisionId { get; private set; }
    public Guid JobId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string RequestJson { get; private set; } = "";
    public DateTime PublishedAtUtc { get; private set; }
    public static CollectionPublication Create(Guid offerId, Guid revisionId, Guid jobId, string fingerprint,
        string hash, string json, DateTime now) => new()
    {
        OfferId = offerId, RevisionId = revisionId, JobId = jobId, Fingerprint = fingerprint,
        RequestHash = hash, RequestJson = json,
        // PostgreSQL timestamps have microsecond precision. First and replayed receipts must be identical.
        PublishedAtUtc = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc)
    };
}
