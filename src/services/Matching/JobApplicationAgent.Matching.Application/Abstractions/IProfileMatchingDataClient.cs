using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IProfileMatchingDataClient
{
    Task<ProfileMatchingData?> GetAsync(
        CancellationToken cancellationToken = default);
}
