using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ResumeSeriesRepository(DocumentsDbContext db) : IResumeSeriesRepository
{
    private IQueryable<ResumeSeries> Series(Guid ownerId) => db.ResumeSeries.AsNoTracking().Where(x => x.OwnerId == ownerId);
    private IQueryable<ResumeSeriesSummary> Summaries(IQueryable<ResumeSeries> series, Guid ownerId) => series
        .Select(x => new ResumeSeriesSummary(x.Id, x.Name, x.CreatedAtUtc,
            db.ResumeVersions.Count(v => v.SeriesId == x.Id && v.OwnerId == ownerId)));
    private IQueryable<ResumeSeriesVersion> Versions(Guid ownerId, Guid seriesId) => db.ResumeVersions.AsNoTracking()
        .Where(x => x.OwnerId == ownerId && x.SeriesId == seriesId);
    private IQueryable<ResumeSeriesVersionMetadata> Metadata(IQueryable<ResumeSeriesVersion> versions, Guid ownerId) =>
        from version in versions
        join document in db.Resumes.AsNoTracking() on version.ResumeId equals document.Id
        where document.OwnerId == ownerId
        select new ResumeSeriesVersionMetadata(version.SeriesId, version.VersionNumber, version.Label, version.AddedAtUtc,
            new ResumeMetadata(document.Id, document.FileName, document.MediaType, document.Length, document.Sha256, document.UploadedAtUtc, document.Status));
    public Task<ResumeSeriesSummary?> GetAsync(Guid ownerId, Guid seriesId, CancellationToken ct) =>
        Summaries(Series(ownerId).Where(x => x.Id == seriesId), ownerId).SingleOrDefaultAsync(ct);
    public Task<ResumeSeriesSummary?> GetForResumeAsync(Guid ownerId, Guid resumeId, CancellationToken ct) =>
        Summaries(Series(ownerId).Where(x => db.ResumeVersions.Any(v => v.OwnerId == ownerId && v.ResumeId == resumeId && v.SeriesId == x.Id)), ownerId).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ResumeSeriesSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct) =>
        await Summaries(Series(ownerId).OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size), ownerId).ToArrayAsync(ct);
    public async Task<IReadOnlyList<ResumeSeriesVersionMetadata>?> GetVersionsAsync(Guid ownerId, Guid seriesId, int page, int size, CancellationToken ct)
    {
        if (!await db.ResumeSeries.AnyAsync(x => x.Id == seriesId && x.OwnerId == ownerId, ct)) return null;
        return await Metadata(Versions(ownerId, seriesId).OrderByDescending(x => x.VersionNumber).Skip((page - 1) * size).Take(size), ownerId).ToArrayAsync(ct);
    }
    public Task<ResumeSeriesVersionMetadata?> GetVersionAsync(Guid ownerId, Guid seriesId, int number, CancellationToken ct) =>
        Metadata(Versions(ownerId, seriesId).Where(x => x.VersionNumber == number), ownerId).SingleOrDefaultAsync(ct);
    public async Task<ResumeSeriesWriteResult?> CreateAsync(Guid ownerId, string name, Guid resumeId, string? label, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockResume(resumeId, ct);
        if (!await db.Resumes.AnyAsync(x => x.Id == resumeId && x.OwnerId == ownerId, ct)) return null;
        var existing = await db.ResumeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ResumeId == resumeId && x.OwnerId == ownerId, ct);
        if (existing is not null)
        {
            var summary = (await GetAsync(ownerId, existing.SeriesId, ct))!;
            if (existing.VersionNumber != 1 || summary.Name != name || existing.Label != label)
                throw new ResumeSeriesConflictException("This resume already belongs to a series with different initial metadata.");
            await transaction.CommitAsync(ct);
            return new(summary, (await GetVersionAsync(ownerId, existing.SeriesId, 1, ct))!, false);
        }
        var series = ResumeSeries.Create(ownerId, name, now);
        db.ResumeSeries.Add(series);
        db.ResumeVersions.Add(ResumeSeriesVersion.Create(ownerId, series.Id, resumeId, 1, label, now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new((await GetAsync(ownerId, series.Id, ct))!, (await GetVersionAsync(ownerId, series.Id, 1, ct))!, true);
    }
    public async Task<ResumeSeriesWriteResult?> AddVersionAsync(Guid ownerId, Guid seriesId, Guid resumeId, string? label, DateTime now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Always take the document lock before the series lock, including competing series assignments.
        await LockResume(resumeId, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({seriesId.ToString()}, 31))", ct);
        if (!await db.ResumeSeries.AnyAsync(x => x.Id == seriesId && x.OwnerId == ownerId, ct) ||
            !await db.Resumes.AnyAsync(x => x.Id == resumeId && x.OwnerId == ownerId, ct)) return null;
        var existing = await db.ResumeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ResumeId == resumeId && x.OwnerId == ownerId, ct);
        if (existing is not null)
        {
            if (existing.SeriesId != seriesId || existing.Label != label)
                throw new ResumeSeriesConflictException("A resume's series assignment and label are immutable.");
            await transaction.CommitAsync(ct);
            return new((await GetAsync(ownerId, seriesId, ct))!, (await GetVersionAsync(ownerId, seriesId, existing.VersionNumber, ct))!, false);
        }
        var last = await db.ResumeVersions.Where(x => x.SeriesId == seriesId && x.OwnerId == ownerId).MaxAsync(x => x.VersionNumber, ct);
        if (last >= 1000) throw new ResumeSeriesConflictException("A series can contain at most 1000 versions.");
        var version = ResumeSeriesVersion.Create(ownerId, seriesId, resumeId, last + 1, label, now);
        db.ResumeVersions.Add(version);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new((await GetAsync(ownerId, seriesId, ct))!, (await GetVersionAsync(ownerId, seriesId, version.VersionNumber, ct))!, true);
    }
    private Task<int> LockResume(Guid resumeId, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({resumeId.ToString()}, 30))", ct);
}
