using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class PreparedDraftRepository(DocumentsDbContext db) : IPreparedDraftRepository
{
    public Task<PreparedDocumentDraft?> GetByRequestAsync(Guid ownerId, Guid requestId, CancellationToken ct) =>
        db.PreparedDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.RequestId == requestId, ct);
    public Task<PreparedDocumentDraft?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) =>
        db.PreparedDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == id, ct);
    public async Task<IReadOnlyList<PreparedDraftSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct) =>
        await db.PreparedDrafts.AsNoTracking().Where(x => x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).Select(x => new PreparedDraftSummary(x.Id, x.RequestId, x.ResumeId, x.JobId,
                x.Kind, x.Status, x.SnapshotSha256, x.CreatedAtUtc)).ToArrayAsync(ct);
    public async Task<DraftResumeSnapshot?> LoadSourceAsync(Guid ownerId, Guid resumeId, Guid[] factIds, CancellationToken ct)
    {
        var resume = await db.Resumes.AsNoTracking().Where(x => x.OwnerId == ownerId && x.Id == resumeId)
            .Select(x => new ResumeMetadata(x.Id, x.FileName, x.MediaType, x.Length, x.Sha256, x.UploadedAtUtc, x.Status)).SingleOrDefaultAsync(ct);
        if (resume is null) return null;
        var facts = await db.CandidateFacts.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ResumeId == resumeId && factIds.Contains(x.Id)).ToArrayAsync(ct);
        if (facts.Length != factIds.Length) return null;
        var version = await db.ResumeVersions.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ResumeId == resumeId)
            .Select(x => new DraftVersionIdentity(x.SeriesId, x.VersionNumber, x.Label)).SingleOrDefaultAsync(ct);
        var byId = facts.ToDictionary(x => x.Id);
        return new(resume, version, factIds.Select(id =>
        {
            var fact = byId[id];
            return new DraftFactSnapshot(CandidateFactDto.From(fact), fact.ProfileDatesJson is null ? null :
                JsonSerializer.Deserialize<ConfirmProfileDatesCommand>(fact.ProfileDatesJson), fact.ProfileDatesConfirmedByOwnerId, fact.ProfileDatesConfirmedAtUtc);
        }).ToArray());
    }
    public async Task<(PreparedDocumentDraft Draft, bool Created)> SaveOrGetAsync(PreparedDocumentDraft draft, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({draft.OwnerId.ToString() + draft.RequestId.ToString()}, 40))", ct);
        var existing = await GetByRequestAsync(draft.OwnerId, draft.RequestId, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != draft.RequestHash) throw new DraftConflictException("This request ID already belongs to a different selection.");
            await transaction.CommitAsync(ct); return (existing, false);
        }
        db.PreparedDrafts.Add(draft); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return (draft, true);
    }
}
