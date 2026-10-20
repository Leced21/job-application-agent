using System.Diagnostics;
using JobApplicationAgent.Collection.Application.Events;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using JobApplicationAgent.Observability;

namespace JobApplicationAgent.Collection.Infrastructure.Events;

public sealed record CollectionOutboxDispatchResult(Guid EventId, bool Published, string? FailureCode, DateTime? RetryAtUtc);

public sealed class CollectionOutboxDispatchService(CollectionDbContext db, ICollectionEventPublisher publisher, TimeProvider clock)
{
    public async Task<CollectionOutboxDispatchResult?> DispatchOneAsync(CancellationToken cancellationToken)
    {
        var startedAt = EventTransportMetrics.StartMeasurement();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var message = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM collection_outbox
            WHERE "DispatchedAtUtc" IS NULL
              AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
            ORDER BY "OccurredAtUtc", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);
        if (message is null) return null;

        string? failureCode = null;
        using var activity = EventTransportMetrics.ActivitySource.StartActivity("collection.outbox.publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", "jobapplication.events");
        activity?.SetTag("messaging.operation.type", "publish");
        try
        {
            await publisher.PublishAsync(message, cancellationToken);
            message.MarkDispatched(clock.GetUtcNow().UtcDateTime);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("messaging.outcome", "cancelled");
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            failureCode = exception.GetType().Name;
            activity?.SetTag("messaging.outcome", "failed");
            activity?.SetStatus(ActivityStatusCode.Error, "publish_failed");
            activity?.AddEvent(new ActivityEvent("messaging.publish.failed",
                tags: new ActivityTagsCollection { { "error.type", failureCode } }));
            var attempts = message.AttemptCount + 1;
            var failedAt = clock.GetUtcNow().UtcDateTime;
            var delaySeconds = Math.Min(300, Math.Pow(2, Math.Min(attempts, 9)));
            message.RecordFailedAttempt(failedAt, failedAt.AddSeconds(delaySeconds), failureCode);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        activity?.SetTag("messaging.outcome", failureCode is null ? "published" : "failed");
        EventTransportMetrics.RecordOutboxDispatch(startedAt, "collection", failureCode is null);
        return new(message.Id, failureCode is null, failureCode, message.NextAttemptAtUtc);
    }
}
