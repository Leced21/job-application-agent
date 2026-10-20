using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class DocumentCompositionRepository(DocumentsDbContext db) : IDocumentCompositionRepository
{
    public Task<DocumentComposition?> GetAsync(Guid ownerId, Guid draftId, CancellationToken ct) => db.Compositions.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.DraftId == draftId, ct);
    public async Task<(DocumentComposition Composition, bool Created)> SaveOrGetAsync(DocumentComposition composition, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({composition.DraftId.ToString()}, 41))", ct);
        var existing = await GetAsync(composition.OwnerId, composition.DraftId, ct);
        if (existing is not null)
        {
            if (existing.SourceSnapshotSha256 != composition.SourceSnapshotSha256) throw new DraftConflictException("The frozen source identity differs from the existing composition.");
            await transaction.CommitAsync(ct); return (existing, false);
        }
        db.Compositions.Add(composition); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (composition, true);
    }
    public async Task<DocumentComposition?> ReviewAsync(Guid ownerId, Guid draftId, CompositionReviewCommand command, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var composition = await db.Compositions.FromSqlInterpolated(
            $"SELECT * FROM document_compositions WHERE \"OwnerId\" = {ownerId} AND \"DraftId\" = {draftId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (composition is null) return null;
        composition.Review(ownerId, command.ContentSha256, command.Decision, command.Note, now);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return composition;
    }
    public async Task<IReadOnlyList<CompositionSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct) =>
        await db.Compositions.AsNoTracking().Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.DraftId)
            .Skip((page - 1) * size).Take(size).Select(x => new CompositionSummary(x.DraftId, x.Kind, x.TemplateVersion, x.SourceSnapshotSha256,
                x.ContentSha256, x.CreatedAtUtc, x.Decision, x.ReviewedByOwnerId, x.ReviewedAtUtc, x.ReviewNote)).ToArrayAsync(ct);
}
