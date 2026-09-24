using FluentValidation;
using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Application.Exceptions;
using JobApplicationAgent.Job.Domain.Enums;
namespace JobApplicationAgent.Job.Application.Jobs.Status;
public sealed record ChangeJobStatusCommand(JobStatus Status);
public sealed class ChangeJobStatusHandler(IJobRepository repository)
{
    public async Task<JobDto> HandleAsync(Guid id, ChangeJobStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Status)) throw new ValidationException("Invalid job offer status.");
        var offer = await repository.GetByIdAsync(id, cancellationToken) ?? throw new JobNotFoundException(id);
        offer.ChangeStatus(command.Status);
        await repository.SaveChangesAsync(cancellationToken);
        return offer.ToDto();
    }
}
