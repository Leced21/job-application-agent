using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IJobMatchingDataClient
{
    Task<JobMatchingData?> GetByIdAsync( Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobMatchingData>> GetPageAsync( int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);
}
