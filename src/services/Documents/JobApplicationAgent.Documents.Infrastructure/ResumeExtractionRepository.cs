using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ResumeExtractionRepository(DocumentsDbContext db) : IResumeExtractionRepository
{
    public Task<ResumeTextExtraction?> GetAsync(Guid ownerId, Guid resumeId, string version, CancellationToken ct) =>
        db.Extractions.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.ResumeId == resumeId && x.EngineVersion == version, ct);
    public async Task<ResumeTextExtraction> SaveOrGetAsync(ResumeTextExtraction extraction, CancellationToken ct)
    {
        db.Extractions.Add(extraction);
        try { await db.SaveChangesAsync(ct); return extraction; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_resume_text_extractions_ResumeId_EngineVersion" })
        {
            db.Entry(extraction).State = EntityState.Detached;
            return (await GetAsync(extraction.OwnerId, extraction.ResumeId, extraction.EngineVersion, ct))!;
        }
    }
}
