using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class DocumentApplicationBindingRepository(DocumentsDbContext db) : IDocumentApplicationBindingRepository
{
    public Task<DocumentApplicationBinding?> GetAsync(Guid ownerId, Guid exportId, CancellationToken ct) => db.ApplicationBindings.AsNoTracking()
        .SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.ExportId == exportId, ct);
    public async Task<DocumentApplicationBinding> SaveOrGetAsync(DocumentApplicationBinding binding, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({binding.ExportId.ToString()}, 43))", ct);
        var existing = await GetAsync(binding.OwnerId, binding.ExportId, ct);
        if (existing is not null)
        {
            if (existing.ProfileId != binding.ProfileId || existing.DraftId != binding.DraftId || existing.JobId != binding.JobId || existing.FileSha256 != binding.FileSha256 ||
                existing.CompositionSha256 != binding.CompositionSha256 || existing.SnapshotSha256 != binding.SnapshotSha256 || existing.Kind != binding.Kind)
                throw new DraftConflictException("This export already has a different confirmed binding.");
            await tx.CommitAsync(ct); return existing;
        }
        db.ApplicationBindings.Add(binding); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return binding;
    }
}
