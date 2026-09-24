using JobApplicationAgent.Job.Application.Abstractions;

namespace JobApplicationAgent.Job.Application.Jobs.Update;

public sealed class UpdateJobHandler
{
    private readonly IJobRepository _repository;

    public UpdateJobHandler(
        IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<JobDto?> HandleAsync(
        Guid id,
        UpdateJobCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (job is null)
        {
            return null;
        }

        job.Update(
            command.Title,
            command.CompanyName,
            command.Location,
            command.WorkMode,
            command.ContractType,
            command.SalaryMin,
            command.SalaryMax,
            command.SalaryCurrency,
            command.Description,
            command.Source,
            command.SourceUrl,
            command.PublishedAtUtc);

        await _repository.SaveChangesAsync(
            cancellationToken);

        return job.ToDto();
    }
}