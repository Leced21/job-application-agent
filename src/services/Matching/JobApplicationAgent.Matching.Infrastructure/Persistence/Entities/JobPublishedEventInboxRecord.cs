namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;

public sealed class JobPublishedEventInboxRecord
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid JobId { get; set; }
    public string PayloadSha256 { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
