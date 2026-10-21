using JobApplicationAgent.Matching.Application.Events;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IJobPublishedEventInbox
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> TryRecordProcessedAsync(JobPublishedV1 message, string payloadSha256, MatchingResult? result,
        CancellationToken cancellationToken = default);
}
