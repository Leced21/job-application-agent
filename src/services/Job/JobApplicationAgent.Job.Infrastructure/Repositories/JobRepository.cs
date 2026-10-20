using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Job.Infrastructure.Repositories;

public sealed class JobRepository
    : IJobRepository
{
    private readonly JobDbContext _dbContext;

    public JobRepository(
        JobDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<JobEntity>> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobEntity>> GetPageByStatusesAsync(
        IReadOnlyCollection<JobStatus> statuses,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        if (page is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (statuses.Any(status => !Enum.IsDefined(status)))
            throw new ArgumentException("Unknown job status.", nameof(statuses));

        cancellationToken.ThrowIfCancellationRequested();
        if (statuses.Count == 0)
            return [];

        var allowedStatuses = statuses.Distinct().ToArray();
        return await _dbContext.Jobs
            .AsNoTracking()
            .Where(job => allowedStatuses.Contains(job.Status))
            .OrderByDescending(job => job.CreatedAtUtc)
            .ThenBy(job => job.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Jobs
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        JobEntity job,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Jobs.AddAsync(
            job,
            cancellationToken);
    }

    public void Delete(JobEntity job)
    {
        _dbContext.Jobs.Remove(job);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}
