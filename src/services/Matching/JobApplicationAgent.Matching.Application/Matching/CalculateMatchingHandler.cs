
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class CalculateMatchingHandler(
    IProfileMatchingDataClient profileClient,
    IJobMatchingDataClient jobClient,
    IMatchingScoringEngine scoringEngine)
{
    public async Task<MatchingResult?> HandleAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job ID is required.",
                nameof(jobId));
        }

        var job = await jobClient.GetByIdAsync(
            jobId,
            cancellationToken);

        if (job is null)
            return null;

        var profile = await profileClient.GetAsync(
            cancellationToken);

        if (profile is null)
            return null;

        var criteria = await scoringEngine.EvaluateAsync(
            profile,
            job,
            cancellationToken);

        var result = new MatchingResult(
            profile.CandidateProfileId,
            job.Id);

        result.Complete(criteria);

        return result;
    }
}
