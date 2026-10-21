using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Applications.Infrastructure;

public sealed class CandidacyRepository(ApplicationsDbContext db) : ICandidacyCreationRepository, ICandidacyTrackingRepository, ICandidacyFollowUpQuery
{
    public async Task<IReadOnlyList<DueFollowUp>> DueAsync(Guid owner, DateTime asOfUtc, int page, int size, CancellationToken ct) =>
        await db.Candidacies.AsNoTracking().Where(x => x.OwnerId == owner && x.NextFollowUpId != null && x.NextFollowUpDueAtUtc <= asOfUtc &&
            (x.Status == (int)CandidacyStatus.Submitted || x.Status == (int)CandidacyStatus.InReview || x.Status == (int)CandidacyStatus.Interview))
            .OrderBy(x => x.NextFollowUpDueAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new DueFollowUp(x.Id, x.JobTitle, x.CompanyName, (CandidacyStatus)x.Status, x.Revision,
                x.NextFollowUpId!.Value, x.NextFollowUpDueAtUtc!.Value, (ContactChannel)x.NextFollowUpChannel!.Value, x.NextFollowUpNote!)).ToArrayAsync(ct);
    public async Task<Candidacy?> FindByRequestAsync(Guid owner, Guid request, CancellationToken ct) =>
        Read(await (from r in db.Requests.AsNoTracking() join c in db.Candidacies.AsNoTracking() on r.CandidacyId equals c.Id
            where r.OwnerId == owner && c.OwnerId == owner && r.RequestId == request select c).SingleOrDefaultAsync(ct));
    public async Task<Candidacy?> FindByIdentityAsync(Guid owner, Guid profile, Guid job, CancellationToken ct) =>
        Read(await db.Candidacies.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.ProfileId == profile && x.JobId == job, ct));
    public async Task<Candidacy?> GetAsync(Guid owner, Guid id, CancellationToken ct) =>
        Read(await db.Candidacies.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.Id == id, ct));
    public async Task<IReadOnlyList<CandidacySummary>> ListAsync(Guid owner, int page, int size, CandidacyStatus? status, CancellationToken ct)
    {
        var query = db.Candidacies.AsNoTracking().Where(x => x.OwnerId == owner);
        if (status.HasValue) query = query.Where(x => x.Status == (int)status.Value);
        return await query
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new CandidacySummary(x.Id, x.ProfileId, x.JobId, x.JobTitle, x.CompanyName, (CandidacyStatus)x.Status,
                x.Revision, x.CreatedAtUtc, x.UpdatedAtUtc)).ToArrayAsync(ct);
    }

    public async Task<Candidacy> InsertOrGetAsync(Guid request, Candidacy candidate, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Always request first, business identity second: shared order avoids deadlocks.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({candidate.OwnerId.ToString() + request}, 71))", ct);
        var replay = await FindByRequestAsync(candidate.OwnerId, request, ct);
        if (replay is not null)
        {
            if (replay.ProfileId != candidate.ProfileId || replay.JobId != candidate.JobId) throw new CandidacyRequestConflictException();
            await tx.CommitAsync(ct); return replay;
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({candidate.OwnerId.ToString() + candidate.ProfileId + candidate.JobId}, 72))", ct);
        var existing = await FindByIdentityAsync(candidate.OwnerId, candidate.ProfileId, candidate.JobId, ct);
        if (existing is not null) candidate = existing;
        else db.Candidacies.Add(ToRow(candidate));
        db.Requests.Add(new() { OwnerId = candidate.OwnerId, RequestId = request, CandidacyId = candidate.Id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return candidate;
    }

    public async Task<Candidacy?> MutateAsync(Guid owner, Guid id, Action<Candidacy> mutation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Candidacies.FromSqlInterpolated($"SELECT * FROM \"Candidacies\" WHERE \"Id\" = {id} AND \"OwnerId\" = {owner} FOR UPDATE")
            .AsTracking().SingleOrDefaultAsync(ct);
        if (row is null) { await tx.CommitAsync(ct); return null; }
        var c = Read(row)!; mutation(c);
        row.HistoryJson = JsonSerializer.Serialize(c.Events); row.Status = (int)c.Status; row.Revision = c.Revision;
        row.UpdatedAtUtc = c.Events[^1].AtUtc;
        ProjectFollowUp(row, c);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return c;
    }
    private static CandidacyRow ToRow(Candidacy c)
    {
        var row = new CandidacyRow {
        Id = c.Id, OwnerId = c.OwnerId, ProfileId = c.ProfileId, JobId = c.JobId,
        JobTitle = c.JobTitle, CompanyName = c.CompanyName, HistoryJson = JsonSerializer.Serialize(c.Events),
        Status = (int)c.Status, Revision = c.Revision, CreatedAtUtc = c.Events[0].AtUtc, UpdatedAtUtc = c.Events[^1].AtUtc
        };
        ProjectFollowUp(row, c); return row;
    }
    private static Candidacy? Read(CandidacyRow? row)
    {
        if (row is null) return null;
        var c = Candidacy.Restore(row.Id, row.OwnerId, row.ProfileId, row.JobId, row.JobTitle, row.CompanyName,
            JsonSerializer.Deserialize<CandidacyEvent[]>(row.HistoryJson) ?? throw new InvalidDataException("Missing history."));
        if ((int)c.Status != row.Status || c.Revision != row.Revision) throw new InvalidDataException("Invalid stored state.");
        var pending = c.PendingFollowUp?.Plan;
        if (pending?.Id != row.NextFollowUpId || pending?.DueAtUtc != row.NextFollowUpDueAtUtc ||
            (int?)pending?.Channel != row.NextFollowUpChannel || pending?.Note != row.NextFollowUpNote)
            throw new InvalidDataException("Invalid stored follow-up projection.");
        return c;
    }
    private static void ProjectFollowUp(CandidacyRow row, Candidacy c)
    {
        var pending = c.PendingFollowUp?.Plan;
        row.NextFollowUpId = pending?.Id; row.NextFollowUpDueAtUtc = pending?.DueAtUtc;
        row.NextFollowUpChannel = (int?)pending?.Channel; row.NextFollowUpNote = pending?.Note;
    }
}
