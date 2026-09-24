using JobApplicationAgent.Job.Application.Abstractions;

namespace JobApplicationAgent.Job.Application.Jobs.Delete;

public sealed class DeleteJobHandler
{
    private readonly IJobRepository _repository;

    public DeleteJobHandler(
        IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> HandleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (job is null)
        {
            return false;
        }

        _repository.Delete(job);

        await _repository.SaveChangesAsync(
            cancellationToken);

        return true;
    }
}