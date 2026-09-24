using JobApplicationAgent.Job.Application.Abstractions;

namespace JobApplicationAgent.Job.Application.Jobs.Get;

public sealed class GetJobsHandler
{
    private readonly IJobRepository _repository;

    public GetJobsHandler(IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<JobDto>> HandleAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100)
            throw new FluentValidation.ValidationException("Page must be between 1 and 1000000, and pageSize between 1 and 100.");
        var offers = await _repository.GetPageAsync(page, pageSize, cancellationToken);
        return offers.Select(offer => offer.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<JobDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var jobs =
            await _repository.GetAllAsync(cancellationToken);

        return jobs
            .Select(job => job.ToDto())
            .ToList();
    }
}