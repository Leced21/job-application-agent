using System.Text.Json;
using System.Diagnostics;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Events;

// Acquisition only: this fact is not approval or permission to add a Job or calculate a match.
public sealed record OfferRevisionRecordedV1(Guid EventId, string EventType, int SchemaVersion,
    DateTime OccurredAtUtc, string Producer, Guid OfferId, Guid RevisionId, string Fingerprint,
    string NormalizationVersion, string SourceName)
{
    public const string Type = "collection.offer-revision-recorded.v1";
    public static CollectionOutboxMessage CreateMessage(OfferRevision revision)
    {
        var occurred = new DateTime(revision.CollectedAtUtc.Ticks - revision.CollectedAtUtc.Ticks % 10, DateTimeKind.Utc);
        var payload = new OfferRevisionRecordedV1(Guid.NewGuid(), Type, 1, occurred, "Collection",
            revision.OfferId, revision.Id, revision.Fingerprint, revision.NormalizationVersion, revision.ReadContent().SourceName);
        return CollectionOutboxMessage.Create(payload.EventId, revision.Id, Type, 1, occurred,
            JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            Activity.Current?.GetTagItem("correlation.id") as string);
    }
}
