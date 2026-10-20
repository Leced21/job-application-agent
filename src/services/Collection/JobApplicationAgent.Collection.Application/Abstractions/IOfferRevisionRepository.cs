using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Abstractions;

public interface IOfferRevisionRepository
{
    Task<OfferRevision?> GetAsync(Guid offerId, Guid revisionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OfferRevision>> GetPageAsync(Guid offerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<OfferRevision?> ReviewAsync(Guid offerId, Guid revisionId, string decision, DateTime now, Guid reviewerOwnerId, CancellationToken cancellationToken);
}
