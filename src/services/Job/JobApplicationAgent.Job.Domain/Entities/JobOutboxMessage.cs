namespace JobApplicationAgent.Job.Domain.Entities;

public sealed class JobOutboxMessage
{
    private JobOutboxMessage() { }
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public string EventType { get; private set; } = "";
    public int SchemaVersion { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string PayloadJson { get; private set; } = "";
    public string? CorrelationId { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public string? LastFailureCode { get; private set; }

    public static JobOutboxMessage Create(Guid id, Guid jobId, string eventType, int schemaVersion,
        DateTime occurredAtUtc, string payloadJson, string? correlationId = null)
    {
        if (id == Guid.Empty || jobId == Guid.Empty || string.IsNullOrWhiteSpace(eventType) || eventType.Length > 150 ||
            schemaVersion < 1 || occurredAtUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("A Job outbox event requires valid identities, contract and UTC time.");
        if (correlationId is not null && !Guid.TryParse(correlationId, out _))
            throw new ArgumentException("An outbox correlation ID must be a GUID.", nameof(correlationId));
        return new() { Id = id, JobId = jobId, EventType = eventType, SchemaVersion = schemaVersion,
            OccurredAtUtc = occurredAtUtc, PayloadJson = payloadJson,
            CorrelationId = correlationId is null ? null : Guid.Parse(correlationId).ToString("D") };
    }

    public void MarkDispatched(DateTime atUtc)
    {
        if (atUtc.Kind != DateTimeKind.Utc || DispatchedAtUtc is not null)
            throw new ArgumentException("Only an undispatched event can be acknowledged with a UTC timestamp.");
        DispatchedAtUtc = atUtc; NextAttemptAtUtc = null; LastFailureCode = null;
    }

    public void RecordFailedAttempt(DateTime failedAtUtc, DateTime retryAtUtc, string failureCode)
    {
        if (failedAtUtc.Kind != DateTimeKind.Utc || retryAtUtc.Kind != DateTimeKind.Utc || retryAtUtc < failedAtUtc ||
            DispatchedAtUtc is not null || string.IsNullOrWhiteSpace(failureCode) || failureCode.Length > 80)
            throw new ArgumentException("A Job outbox retry requires UTC times and a bounded failure code.");
        AttemptCount = checked(AttemptCount + 1); NextAttemptAtUtc = retryAtUtc; LastFailureCode = failureCode;
    }
}
