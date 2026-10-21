using System.Text.Json;
using JobApplicationAgent.Collection.Application.Events;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class OutboxContractTests
{
    [Fact]
    public void RevisionRecorded_ShouldExposeVersionedAcquisitionMetadata_WithStableEnvelope()
    {
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc).AddTicks(7);
        var offer = new OfferNormalizer().Normalize(new("Engineer", "Company", Description: "Private description", SourceName: "LocalJsonExport"), now);
        var revision = OfferRevision.From(offer, offer.Id);
        var message = OfferRevisionRecordedV1.CreateMessage(revision);
        using var document = JsonDocument.Parse(message.PayloadJson);
        var root = document.RootElement;
        Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(revision.Id, root.GetProperty("revisionId").GetGuid());
        Assert.Equal(offer.Id, root.GetProperty("offerId").GetGuid());
        Assert.Equal(OfferRevisionRecordedV1.Type, root.GetProperty("eventType").GetString());
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Collection", root.GetProperty("producer").GetString());
        Assert.Equal("LocalJsonExport", root.GetProperty("sourceName").GetString());
        Assert.Equal(revision.Fingerprint, root.GetProperty("fingerprint").GetString());
        Assert.Equal(message.OccurredAtUtc, root.GetProperty("occurredAtUtc").GetDateTime());
        Assert.Equal(0, message.OccurredAtUtc.Ticks % 10);
        Assert.Null(message.DispatchedAtUtc);
        Assert.False(root.TryGetProperty("description", out _));
        Assert.False(root.TryGetProperty("jobId", out _));
        Assert.Equal("Pending", revision.Decision);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("date")]
    [InlineData("version")]
    public void Outbox_ShouldRejectInvalidEnvelope(string invalid)
    {
        Assert.Throws<ArgumentException>(() => CollectionOutboxMessage.Create(invalid == "id" ? Guid.Empty : Guid.NewGuid(),
            Guid.NewGuid(), OfferRevisionRecordedV1.Type, invalid == "version" ? 0 : 1,
            invalid == "date" ? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) : DateTime.UtcNow, "{}"));
    }

    [Fact]
    public void OutboxFailure_ShouldPersistBoundedRetryAndDispatchAcknowledgement()
    {
        var created = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var message = CollectionOutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid(), OfferRevisionRecordedV1.Type, 1, created, "{}");
        var retryAt = created.AddSeconds(8);

        message.RecordFailedAttempt(created, retryAt, "BrokerUnavailableException");

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(retryAt, message.NextAttemptAtUtc);
        Assert.Equal("BrokerUnavailableException", message.LastFailureCode);
        message.MarkDispatched(retryAt.AddSeconds(1));
        Assert.NotNull(message.DispatchedAtUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.LastFailureCode);
    }

    [Fact]
    public void Outbox_ShouldNormalizeCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var message = CollectionOutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid(), OfferRevisionRecordedV1.Type, 1,
            DateTime.UtcNow, "{}", correlationId.ToString("N"));

        Assert.Equal(correlationId.ToString("D"), message.CorrelationId);
    }
}
