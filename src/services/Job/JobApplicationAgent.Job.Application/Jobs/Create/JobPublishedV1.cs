using System.Text.Json;
using System.Diagnostics;
using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.Application.Jobs.Create;

public interface IJobEventPublisher
{
    Task PublishAsync(JobOutboxMessage message, CancellationToken cancellationToken);
}

// This event is emitted only after an approved Collection revision becomes a canonical Job.
public sealed record JobPublishedV1(Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
    string Producer, Guid JobId, Guid CollectionOfferId, Guid CollectionRevisionId, string Fingerprint)
{
    public const string Type = "job.published.v1";

    public static JobOutboxMessage CreateMessage(CollectionPublication receipt)
    {
        var occurred = new DateTime(receipt.PublishedAtUtc.Ticks - receipt.PublishedAtUtc.Ticks % 10, DateTimeKind.Utc);
        var payload = new JobPublishedV1(Guid.NewGuid(), Type, 1, occurred, "Job", receipt.JobId,
            receipt.OfferId, receipt.RevisionId, receipt.Fingerprint.ToLowerInvariant());
        return JobOutboxMessage.Create(payload.EventId, receipt.JobId, Type, 1, occurred,
            JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            Activity.Current?.GetTagItem("correlation.id") as string);
    }
}
