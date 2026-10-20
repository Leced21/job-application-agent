using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ResumeRepository(DocumentsDbContext db) : IResumeRepository
{
    public async Task AddAsync(ResumeDocument document, CancellationToken ct)
    {
        db.Resumes.Add(document);
        await db.SaveChangesAsync(ct);
    }
    public Task<ResumeDocument?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) =>
        db.Resumes.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, ct);
    public async Task<IReadOnlyList<ResumeMetadata>> GetPageAsync(Guid ownerId, int page, int pageSize, CancellationToken ct) =>
        await db.Resumes.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.UploadedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ResumeMetadata(x.Id, x.FileName, x.MediaType, x.Length, x.Sha256, x.UploadedAtUtc, x.Status)).ToArrayAsync(ct);
}
