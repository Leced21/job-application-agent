using FluentValidation;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.Application.Jobs.Get;

public sealed class GetRecommendableJobsHandler(IJobRepository repository)
{
    public async Task<IReadOnlyList<JobDto>> HandleAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            throw new ValidationException(
                "Page must be between 1 and 1000000, and pageSize between 1 and 100.");

        var jobs = await repository.GetPageByStatusesAsync(
            [JobStatus.Discovered, JobStatus.Saved], page, pageSize, cancellationToken);

        return jobs.Select(job => job.ToDto()).ToArray();
    }
}
