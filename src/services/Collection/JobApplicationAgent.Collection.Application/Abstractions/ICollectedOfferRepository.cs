using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Abstractions;

public sealed record ImportPersistenceResult(CollectedOffer Offer, bool IsDuplicate, bool ContentChanged);
public sealed record CollectedOfferPage(IReadOnlyList<CollectedOffer> Items, int TotalCount);
public interface ICollectedOfferRepository
{
    Task<ImportPersistenceResult> ImportAsync(CollectedOffer offer, CancellationToken cancellationToken = default);
    Task<CollectedOffer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CollectedOfferPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
