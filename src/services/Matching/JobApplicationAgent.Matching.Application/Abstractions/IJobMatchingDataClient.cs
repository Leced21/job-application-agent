using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IJobMatchingDataClient
{
    Task<JobMatchingData?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
