using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IMatchingScoringEngine
{
    Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(
        ProfileMatchingData profile,
        JobMatchingData job,
        CancellationToken cancellationToken = default);
}
