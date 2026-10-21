using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IMatchingResultRepository
{
    Task AddAsync(MatchingResult result, CancellationToken cancellationToken = default);
    Task<MatchingResultDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MatchingResultPage> SearchAsync(MatchingResultFilter filter, CancellationToken cancellationToken = default);
}
