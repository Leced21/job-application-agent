using System.Diagnostics;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class CalculateMatchingHandler( IProfileMatchingDataClient profileClient, IJobMatchingDataClient jobClient, IMatchingScoringEngine scoringEngine, IMatchingResultRepository resultRepository)
{
    public async Task<MatchingResult?> HandleAsync( Guid jobId, CancellationToken cancellationToken = default)
    {
        var result = await CalculateAsync(jobId, cancellationToken);
        if (result is not null)
            await resultRepository.AddAsync(result, cancellationToken);
        return result;
    }

    public async Task<MatchingResult?> CalculateAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job ID is required.",
                nameof(jobId));
        }

        var started = Stopwatch.GetTimestamp();
        var outcome = "failed";
        using var activity = MatchingCalculationMetrics.ActivitySource.StartActivity("matching.calculate", ActivityKind.Internal);
        try
        {
            var job = await jobClient.GetByIdAsync(jobId, cancellationToken);

            if (job is null)
            {
                outcome = "job_not_found";
                return null;
            }

            var profile = await profileClient.GetAsync(cancellationToken);

            if (profile is null)
            {
                outcome = "profile_not_found";
                return null;
            }

            var criteria = await scoringEngine.EvaluateAsync(profile, job, cancellationToken);

            var result = new MatchingResult(profile.CandidateProfileId, job.Id, scoringEngine.Version);
            result.Complete(criteria);
            outcome = "completed";
            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            activity?.SetTag("matching.outcome", outcome);
            if (outcome == "failed")
            {
                activity?.SetStatus(ActivityStatusCode.Error, "calculation_failed");
                activity?.AddEvent(new ActivityEvent("matching.calculation.failed"));
            }
            else if (outcome == "cancelled")
            {
                activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            }
            MatchingCalculationMetrics.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, outcome);
        }
    }
}
