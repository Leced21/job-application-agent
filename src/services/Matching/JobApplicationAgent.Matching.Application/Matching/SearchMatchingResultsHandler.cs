using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class SearchMatchingResultsHandler(
    IMatchingResultRepository repository)
{
    public Task<MatchingResultPage> HandleAsync(
        MatchingResultFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter.Page),
                "Page must be greater than zero.");
        }

        if (filter.PageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter.PageSize),
                "Page size must be between 1 and 100.");
        }

        if (filter.CandidateProfileId == Guid.Empty)
        {
            throw new ArgumentException(
                "Candidate profile ID cannot be empty.");
        }

        if (filter.JobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job ID cannot be empty.");
        }

        return repository.SearchAsync(
            filter,
            cancellationToken);
    }
}
