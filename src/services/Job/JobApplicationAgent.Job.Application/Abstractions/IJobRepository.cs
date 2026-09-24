using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.Application.Abstractions;

public interface IJobRepository
{
    Task<IReadOnlyList<JobEntity>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<JobEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Delete(JobEntity job);

    Task AddAsync(JobEntity job,CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobEntity>> GetAllAsync(CancellationToken cancellationToken = default);

}