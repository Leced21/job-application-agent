using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Events;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;

public sealed class JobPublishedEventInbox(MatchingDbContext db, TimeProvider clock) : IJobPublishedEventInbox
{
    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(eventId));
        return db.JobPublishedEvents.AsNoTracking().AnyAsync(x => x.EventId == eventId, cancellationToken);
    }

    public async Task<bool> TryRecordProcessedAsync(JobPublishedV1 message, string payloadSha256,
        MatchingResult? result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();
        if (payloadSha256.Length != 64 || !payloadSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("A SHA-256 payload fingerprint is required.", nameof(payloadSha256));
        if (result is not null && result.JobId != message.JobId)
            throw new ArgumentException("The Matching result must belong to the published Job.", nameof(result));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var processedAt = clock.GetUtcNow().UtcDateTime;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO matching_event_inbox ("EventId", "EventType", "JobId", "PayloadSha256", "OccurredAtUtc", "ProcessedAtUtc")
            VALUES ({message.EventId}, {message.EventType}, {message.JobId}, {payloadSha256}, {message.OccurredAtUtc}, {processedAt})
            ON CONFLICT ("EventId") DO NOTHING
            """, cancellationToken);

        if (inserted == 0)
        {
            var existing = await db.JobPublishedEvents.AsNoTracking().SingleAsync(x => x.EventId == message.EventId, cancellationToken);
            if (existing.EventType != message.EventType || existing.JobId != message.JobId ||
                existing.PayloadSha256 != payloadSha256)
                throw new InvalidOperationException("An event ID was reused with a different Job event envelope.");
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        if (result is not null) db.MatchingResults.Add(ToRecord(result));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static MatchingResultRecord ToRecord(MatchingResult result) => new()
    {
        Id = result.Id,
        CandidateProfileId = result.CandidateProfileId,
        JobId = result.JobId,
        Status = result.Status.ToString(),
        ScoringVersion = result.ScoringVersion,
        OverallScore = result.OverallScore,
        EvaluationCoverage = result.EvaluationCoverage,
        CreatedAtUtc = result.CreatedAtUtc,
        CompletedAtUtc = result.CompletedAtUtc,
        Criteria = result.Criteria.Select(criterion => new MatchingCriterionRecord
        {
            Id = Guid.NewGuid(),
            MatchingResultId = result.Id,
            Name = criterion.Name,
            Score = criterion.Score,
            Weight = criterion.Weight,
            Explanation = criterion.Explanation,
            Status = criterion.Status.ToString()
        }).ToList()
    };
}
