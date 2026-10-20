using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Domain.Entities;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using JobApplicationAgent.Collection.Application.Events;

namespace JobApplicationAgent.Collection.Infrastructure.Repositories;

public sealed class CollectedOfferRepository(CollectionDbContext db) : ICollectedOfferRepository,
    JobApplicationAgent.Collection.Application.Connectors.ICollectedOfferBatchRepository
{
    public async Task<IReadOnlyList<ImportPersistenceResult>> ImportBatchAsync(IReadOnlyList<CollectedOffer> offers, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var results = new ImportPersistenceResult[offers.Count];
        // Stable lock order prevents two batches with reversed input order from deadlocking.
        foreach (var item in offers.Select((offer, index) => (offer, index)).OrderBy(x => x.offer.DeduplicationKey, StringComparer.Ordinal))
            results[item.index] = await ImportAsync(item.offer, ct);
        await transaction.CommitAsync(ct);
        return results;
    }
    public async Task<ImportPersistenceResult> ImportAsync(CollectedOffer offer, CancellationToken cancellationToken = default)
    {
        var existing = await db.Offers.AsNoTracking().SingleOrDefaultAsync(
            item => item.DeduplicationKey == offer.DeduplicationKey, cancellationToken);
        if (existing is null)
        {
            db.Offers.Add(offer);
            var initialRevision = OfferRevision.From(offer, offer.Id);
            db.Revisions.Add(initialRevision);
            var initialMessage = OfferRevisionRecordedV1.CreateMessage(initialRevision);
            db.OutboxMessages.Add(initialMessage);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new ImportPersistenceResult(offer, false, false);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_collected_offers_DeduplicationKey" })
            {
                // A concurrent import committed the same identity. Detach the failed insert before reading it.
                db.Entry(offer).State = EntityState.Detached;
                db.Entry(initialRevision).State = EntityState.Detached;
                db.Entry(initialMessage).State = EntityState.Detached;
            }
        }

        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var locked = await db.Offers.FromSqlInterpolated(
            $"SELECT * FROM collected_offers WHERE \"DeduplicationKey\" = {offer.DeduplicationKey} FOR UPDATE")
            .AsNoTracking().SingleAsync(cancellationToken);
        if (!await db.Revisions.AnyAsync(x => x.OfferId == locked.Id && x.Fingerprint == offer.ContentFingerprint, cancellationToken))
        {
            var revision = OfferRevision.From(offer, locked.Id);
            db.Revisions.Add(revision);
            db.OutboxMessages.Add(OfferRevisionRecordedV1.CreateMessage(revision));
        }
        await db.SaveChangesAsync(cancellationToken);
        await db.Offers.Where(item => item.DeduplicationKey == offer.DeduplicationKey)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.SeenCount, item => item.SeenCount + 1)
                .SetProperty(item => item.HasPendingChanges, item => item.HasPendingChanges || item.ContentFingerprint != offer.ContentFingerprint)
                .SetProperty(item => item.LastSeenAtUtc, item => item.LastSeenAtUtc < offer.LastSeenAtUtc
                    ? offer.LastSeenAtUtc : item.LastSeenAtUtc), cancellationToken);
        var saved = await db.Offers.AsNoTracking().SingleAsync(
            item => item.DeduplicationKey == offer.DeduplicationKey, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new ImportPersistenceResult(saved, true, saved.ContentFingerprint != offer.ContentFingerprint);
    }

    public Task<CollectedOffer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Offers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<CollectedOfferPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = db.Offers.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.FirstCollectedAtUtc).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new CollectedOfferPage(items, total);
    }
}
