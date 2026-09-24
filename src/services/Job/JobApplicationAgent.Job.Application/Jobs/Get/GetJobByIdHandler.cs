using JobApplicationAgent.Job.Application.Abstractions;

namespace JobApplicationAgent.Job.Application.Jobs.Get;

public sealed class GetJobByIdHandler
{
    private readonly IJobRepository _repository;

    public GetJobByIdHandler(IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<JobDto?> HandleAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var job =
            await _repository.GetByIdAsync(
                id,
                cancellationToken);

        return job?.ToDto();
    }
}