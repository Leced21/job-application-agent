namespace JobApplicationAgent.Collection.Domain.Entities;

public sealed class CollectionOutboxMessage
{
    private CollectionOutboxMessage() { }
    public Guid Id { get; private set; }
    public Guid RevisionId { get; private set; }
    public string EventType { get; private set; } = "";
    public int SchemaVersion { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = "";
    public string? CorrelationId { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastFailureCode { get; private set; }

    public static CollectionOutboxMessage Create(Guid id, Guid revisionId, string type, int version, DateTime occurredAtUtc,
        string json, string? correlationId = null)
    {
        if (id == Guid.Empty || revisionId == Guid.Empty || string.IsNullOrWhiteSpace(type) || version < 1 ||
            occurredAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("An outbox message requires valid identities, type, version, UTC date and payload.");
        if (correlationId is not null && !Guid.TryParse(correlationId, out _))
            throw new ArgumentException("An outbox correlation ID must be a GUID.", nameof(correlationId));
        return new() { Id = id, RevisionId = revisionId, EventType = type, SchemaVersion = version,
            OccurredAtUtc = occurredAtUtc, PayloadJson = json,
            CorrelationId = correlationId is null ? null : Guid.Parse(correlationId).ToString("D") };
    }

    public void MarkDispatched(DateTime atUtc)
    {
        if (atUtc.Kind != DateTimeKind.Utc || DispatchedAtUtc is not null)
            throw new ArgumentException("Only an undispatched message can be acknowledged with a UTC timestamp.");
        DispatchedAtUtc = atUtc;
        NextAttemptAtUtc = null;
        LastFailureCode = null;
    }

    public void RecordFailedAttempt(DateTime atUtc, DateTime retryAtUtc, string failureCode)
    {
        if (atUtc.Kind != DateTimeKind.Utc || retryAtUtc.Kind != DateTimeKind.Utc || retryAtUtc < atUtc ||
            DispatchedAtUtc is not null || string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 80)
            throw new ArgumentException("An outbox retry requires UTC times and a bounded failure code.");
        AttemptCount = checked(AttemptCount + 1);
        NextAttemptAtUtc = retryAtUtc;
        LastFailureCode = failureCode;
    }
}
