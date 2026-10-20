using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class DocumentExportRepository(DocumentsDbContext db) : IDocumentExportRepository
{
    private static System.Linq.Expressions.Expression<Func<DocumentExport, DocumentExportMetadata>> Metadata => x =>
        new(x.Id, x.DraftId, x.Format, x.RendererVersion, x.CompositionSha256, x.FileSha256, x.FileName, x.MediaType,
            x.Length, x.PageCount, x.CreatedAtUtc, x.ApprovedAtUtc);
    public Task<DocumentExport?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => db.Exports.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, ct);
    public Task<DocumentExportMetadata?> GetMetadataAsync(Guid ownerId, Guid id, CancellationToken ct) => db.Exports.AsNoTracking()
        .Where(x => x.OwnerId == ownerId && x.Id == id).Select(Metadata).SingleOrDefaultAsync(ct);
    public Task<DocumentExportMetadata?> FindAsync(Guid ownerId, Guid draftId, string format, CancellationToken ct) => db.Exports.AsNoTracking()
        .Where(x => x.OwnerId == ownerId && x.DraftId == draftId && x.Format == format).Select(Metadata).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<DocumentExportMetadata>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct) =>
        await db.Exports.AsNoTracking().Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).Select(Metadata).ToArrayAsync(ct);
    public async Task<(DocumentExportMetadata Export, bool Created)> SaveOrGetAsync(DocumentExport export, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({export.DraftId.ToString() + export.Format}, 42))", ct);
        var existing = await FindAsync(export.OwnerId, export.DraftId, export.Format, ct);
        if (existing is not null)
        {
            if (existing.CompositionSha256 != export.CompositionSha256) throw new DraftConflictException("An export already exists for different content.");
            await transaction.CommitAsync(ct); return (existing, false);
        }
        db.Exports.Add(export); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (DocumentExportMetadata.From(export), true);
    }
}
