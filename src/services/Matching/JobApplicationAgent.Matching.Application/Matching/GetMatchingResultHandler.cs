using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class GetMatchingResultHandler(IMatchingResultRepository repository)
{
    public Task<MatchingResultDetails?> HandleAsync( Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Matching result ID is required.",nameof(id));
        }
        return repository.GetByIdAsync( id, cancellationToken);
    }
}
