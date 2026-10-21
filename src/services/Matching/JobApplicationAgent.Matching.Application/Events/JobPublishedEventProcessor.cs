using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;

namespace JobApplicationAgent.Matching.Application.Events;

public sealed class JobPublishedEventProcessor(CalculateMatchingHandler calculator, IJobPublishedEventInbox inbox)
{
    public async Task<bool> ProcessAsync(JobPublishedV1 message, string rawPayload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPayload);
        if (await inbox.IsProcessedAsync(message.EventId, cancellationToken))
            return false;

        var result = await calculator.CalculateAsync(message.JobId, cancellationToken);
        return await inbox.TryRecordProcessedAsync(message, message.GetPayloadSha256(rawPayload), result, cancellationToken);
    }
}
