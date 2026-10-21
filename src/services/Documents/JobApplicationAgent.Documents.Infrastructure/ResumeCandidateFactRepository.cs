using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ResumeCandidateFactRepository(DocumentsDbContext db) : IResumeCandidateFactRepository, IProfileFactMappingRepository
{
    public async Task<ResumeCandidateFact?> ConfirmAsync(Guid ownerId, Guid resumeId, Guid factId, string datesJson, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var fact = await db.CandidateFacts.FromSqlInterpolated(
            $"SELECT * FROM resume_candidate_facts WHERE \"Id\" = {factId} AND \"OwnerId\" = {ownerId} AND \"ResumeId\" = {resumeId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (fact is null) return null;
        fact.ConfirmProfileDates(ownerId, datesJson, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return fact;
    }
    public async Task<(ResumeCandidateFact Fact, bool Created)> SaveOrGetAsync(ResumeCandidateFact fact, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({fact.ResumeId.ToString()}, 0))", ct);
        var existing = await db.CandidateFacts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == fact.OwnerId &&
            x.ExtractionId == fact.ExtractionId && x.Fingerprint == fact.Fingerprint, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return (existing, false); }
        if (await db.CandidateFacts.CountAsync(x => x.OwnerId == fact.OwnerId && x.ResumeId == fact.ResumeId, ct) >= 200)
            throw new FactReviewConflictException("A resume can contain at most 200 candidate facts.");
        db.CandidateFacts.Add(fact);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (fact, true);
    }
    public Task<ResumeCandidateFact?> GetAsync(Guid ownerId, Guid resumeId, Guid factId, CancellationToken ct) =>
        db.CandidateFacts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.ResumeId == resumeId && x.Id == factId, ct);
    public async Task<IReadOnlyList<ResumeCandidateFact>> GetPageAsync(Guid ownerId, Guid resumeId, string? decision, int page, int pageSize, CancellationToken ct) =>
        await db.CandidateFacts.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ResumeId == resumeId && (decision == null || x.Decision == decision))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
    public async Task<ResumeCandidateFact?> ReviewAsync(Guid ownerId, Guid resumeId, Guid factId, string decision, string? note, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var fact = await db.CandidateFacts.FromSqlInterpolated(
            $"SELECT * FROM resume_candidate_facts WHERE \"Id\" = {factId} AND \"OwnerId\" = {ownerId} AND \"ResumeId\" = {resumeId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (fact is null) return null;
        fact.Review(ownerId, decision, note, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return fact;
    }
}
