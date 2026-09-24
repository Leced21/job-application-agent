using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.Application.Jobs;

internal static class JobMappings
{
    internal static JobDto ToDto(this JobEntity job)
    {
        return new JobDto(
            job.Id,
            job.Title,
            job.CompanyName,
            job.Location,
            job.WorkMode,
            job.ContractType,
            job.SalaryMin,
            job.SalaryMax,
            job.SalaryCurrency,
            job.Description,
            job.Source,
            job.SourceUrl,
            job.Status,
            job.PublishedAtUtc,
            job.CreatedAtUtc,
            job.UpdatedAtUtc);
    }
}