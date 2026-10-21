using System.Diagnostics;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using JobApplicationAgent.Observability;

namespace JobApplicationAgent.Job.Infrastructure.Events;

public sealed record JobOutboxDispatchResult(Guid EventId, bool Published, string? FailureCode, DateTime? RetryAtUtc);

public sealed class JobOutboxDispatchService(JobDbContext db, IJobEventPublisher publisher, TimeProvider clock)
{
    public async Task<JobOutboxDispatchResult?> DispatchOneAsync(CancellationToken cancellationToken)
    {
        var startedAt = EventTransportMetrics.StartMeasurement();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var message = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM job_outbox
            WHERE "DispatchedAtUtc" IS NULL
              AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {now})
            ORDER BY "OccurredAtUtc", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).FirstOrDefaultAsync(cancellationToken);
        if (message is null) return null;

        string? failureCode = null;
        using var activity = EventTransportMetrics.ActivitySource.StartActivity("job.outbox.publish", ActivityKind.Producer);
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
            var failedAt = clock.GetUtcNow().UtcDateTime;
            var retryAt = failedAt.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount + 1, 9))));
            message.RecordFailedAttempt(failedAt, retryAt, failureCode);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        activity?.SetTag("messaging.outcome", failureCode is null ? "published" : "failed");
        EventTransportMetrics.RecordOutboxDispatch(startedAt, "job", failureCode is null);
        return new(message.Id, failureCode is null, failureCode, message.NextAttemptAtUtc);
    }
}
