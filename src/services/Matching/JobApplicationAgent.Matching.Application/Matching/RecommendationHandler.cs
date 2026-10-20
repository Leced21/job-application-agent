using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class RecommendationHandler(
    IProfileMatchingDataClient profileClient,
    IJobMatchingDataClient jobClient,
    IMatchingScoringEngine scoringEngine,
    RecommendationRankingService rankingService,
    RecommendationProcessingLimits? processingLimits = null)
{
    private static readonly IComparer<JobRecommendation> BestFirst = Comparer<JobRecommendation>.Create((left, right) =>
    {
        var score = Nullable.Compare(right.RankingScore, left.RankingScore);
        if (score != 0) return score;
        var coverage = right.EvaluationCoverage.CompareTo(left.EvaluationCoverage);
        return coverage != 0 ? coverage : left.JobId.CompareTo(right.JobId);
    });

    public async Task<RecommendationResponse?> HandleAsync(
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be between 1 and 1000000.");
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");

        var limits = processingLimits ?? new RecommendationProcessingLimits();
        limits.Validate();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
        var token = budget.Token;
        token.ThrowIfCancellationRequested();

        var profile = await profileClient.GetAsync(token);
        token.ThrowIfCancellationRequested();
        if (profile is null) return null;

        const int batchSize = 100;
        var skip = (page - 1) * pageSize;
        var keep = skip >= limits.MaxUniqueJobs ? 0 : Math.Min(page * pageSize, limits.MaxUniqueJobs);
        var seen = new HashSet<Guid>();
        // The root is the worst retained result, so it can be evicted in O(log keep).
        var best = new PriorityQueue<JobRecommendation, JobRecommendation>(
            Comparer<JobRecommendation>.Create((left, right) => BestFirst.Compare(right, left)));

        for (var sourcePage = 1; ; sourcePage++)
        {
            token.ThrowIfCancellationRequested();
            var batch = await jobClient.GetRecommendablePageAsync(sourcePage, batchSize, token);
            foreach (var job in batch)
            {
                token.ThrowIfCancellationRequested();
                // First observed version wins if the catalogue moves between source pages.
                if (seen.Contains(job.Id)) continue;
                if (seen.Count == limits.MaxUniqueJobs)
                    throw new RecommendationCapacityExceededException();
                seen.Add(job.Id);

                if (keep == 0) continue;
                var criteria = await scoringEngine.EvaluateAsync(profile, job, token);
                token.ThrowIfCancellationRequested();
                var result = new MatchingResult(profile.CandidateProfileId, job.Id, scoringEngine.Version);
                result.Complete(criteria);
                var recommendation = new JobRecommendation(
                    job.Id, job.Title, job.CompanyName, job.Location, job.WorkMode, job.ContractType,
                    job.SalaryMin, job.SalaryMax, job.SalaryCurrency,
                    result.OverallScore, result.EvaluationCoverage, result.Status.ToString(), result.Criteria)
                {
                    RankingScore = rankingService.Calculate(result.OverallScore, result.EvaluationCoverage),
                    ScoringVersion = result.ScoringVersion
                };

                if (best.Count < keep)
                    best.Enqueue(recommendation, recommendation);
                else
                    best.EnqueueDequeue(recommendation, recommendation);
            }

            if (batch.Count < batchSize) break;
            if (sourcePage == limits.MaxSourcePages)
                throw new RecommendationCapacityExceededException();
        }

        token.ThrowIfCancellationRequested();
        var items = best.UnorderedItems.Select(item => item.Element)
            .OrderBy(item => item, BestFirst).Skip(skip).Take(pageSize).ToArray();
        token.ThrowIfCancellationRequested();

        return new RecommendationResponse(profile.CandidateProfileId, items, page, pageSize)
        {
            TotalCount = seen.Count,
            ScoringVersion = scoringEngine.Version
        };
    }
}
