
using JobApplicationAgent.Matching.Domain.Enums;

namespace JobApplicationAgent.Matching.Domain.Entities;

public sealed class MatchingResult
{
    public const string LegacyScoringVersion = "legacy-v1";
    public string ScoringVersion { get; }
    private readonly List<MatchingCriterion> _criteria = [];

    public Guid Id { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public Guid JobId { get; private set; }
    public MatchingStatus Status { get; private set; }
    public decimal? OverallScore { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public decimal EvaluationCoverage { get; private set; }
    public IReadOnlyCollection<MatchingCriterion> Criteria => _criteria.AsReadOnly();

    public MatchingResult(Guid candidateProfileId, Guid jobId, string scoringVersion = LegacyScoringVersion)
    {
        if (candidateProfileId == Guid.Empty)
            throw new ArgumentException(
                "Candidate profile ID is required.",
                nameof(candidateProfileId));

        if (jobId == Guid.Empty)
            throw new ArgumentException(
                "Job ID is required.",
                nameof(jobId));

        if (string.IsNullOrWhiteSpace(scoringVersion) || scoringVersion.Length > 128)
            throw new ArgumentException("A scoring version of at most 128 characters is required.", nameof(scoringVersion));
        ScoringVersion = scoringVersion;
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        JobId = jobId;
        Status = MatchingStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }



    public void Complete(IEnumerable<MatchingCriterion> criteria)
    {
        if (Status != MatchingStatus.Pending)
            throw new InvalidOperationException(
                "Only pending matching results can be completed.");

        ArgumentNullException.ThrowIfNull(criteria);

        var items = criteria.ToArray();

        if (items.Length == 0)
            throw new ArgumentException(
                "At least one criterion is required.",
                nameof(criteria));

        var evaluatedCriteria = items
            .Where(x =>
                x.Status == MatchingCriterionStatus.Evaluated &&
                x.Weight > 0)
            .ToArray();

        var evaluatedWeight = evaluatedCriteria.Sum(x => x.Weight);
        var totalWeight = items.Where(x => x.Status != MatchingCriterionStatus.NotApplicable).Sum(x => x.Weight);

        EvaluationCoverage = totalWeight > 0
            ? Math.Round(
                evaluatedWeight / totalWeight * 100m,
                2,
                MidpointRounding.AwayFromZero)
            : 0m;

        _criteria.AddRange(items);

        if (evaluatedWeight == 0)
        {
            OverallScore = null;
            Status = MatchingStatus.InsufficientData;
            CompletedAtUtc = DateTime.UtcNow;
            return;
        }

        var weightedScore = evaluatedCriteria.Sum(
            x => x.Score!.Value * x.Weight) / evaluatedWeight;

        OverallScore = Math.Round(
            weightedScore,
            2,
            MidpointRounding.AwayFromZero);

        Status = MatchingStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;


    }



    public void Fail()
    {
        if (Status != MatchingStatus.Pending)
            throw new InvalidOperationException(
                "Only pending matching results can fail.");

        Status = MatchingStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
