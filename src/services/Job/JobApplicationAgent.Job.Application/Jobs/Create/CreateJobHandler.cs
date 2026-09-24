using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using FluentValidation;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.Application.Jobs.Create;

public sealed class CreateJobHandler
{
    private readonly IJobRepository _repository;
    private readonly IValidator<CreateJobCommand> _validator;

    public CreateJobHandler(
        IJobRepository repository, IValidator<CreateJobCommand> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<JobDto> HandleAsync(
        CreateJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(command, cancellationToken);
        var job = JobEntity.Create(
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

        await _repository.AddAsync(
            job,
            cancellationToken);

        await _repository.SaveChangesAsync(
            cancellationToken);

        return job.ToDto();
    }
}