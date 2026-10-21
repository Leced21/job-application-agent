using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Domain.Entities;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Collection.Infrastructure.Repositories;

public sealed class OfferRevisionRepository(CollectionDbContext db) : IOfferRevisionRepository
{
    public Task<OfferRevision?> GetAsync(Guid offerId, Guid revisionId, CancellationToken cancellationToken) =>
        db.Revisions.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offerId && x.Id == revisionId, cancellationToken);
    public async Task<IReadOnlyList<OfferRevision>> GetPageAsync(Guid offerId, int page, int pageSize, CancellationToken cancellationToken) =>
        await db.Revisions.AsNoTracking().Where(x => x.OfferId == offerId)
            .OrderByDescending(x => x.CollectedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);

    public async Task<OfferRevision?> ReviewAsync(Guid offerId, Guid revisionId, string decision, DateTime now, Guid reviewerOwnerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var revision = await db.Revisions.FromSqlInterpolated(
            $"SELECT * FROM offer_revisions WHERE \"Id\" = {revisionId} AND \"OfferId\" = {offerId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (revision is null) return null;
        revision.Review(decision, now, reviewerOwnerId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return revision;
    }
}
