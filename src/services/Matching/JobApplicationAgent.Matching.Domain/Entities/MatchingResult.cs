
using JobApplicationAgent.Matching.Domain.Enums;

namespace JobApplicationAgent.Matching.Domain.Entities;

public sealed class MatchingResult
{
    private readonly List<MatchingCriterion> _criteria = [];

    public Guid Id { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public Guid JobId { get; private set; }
    public MatchingStatus Status { get; private set; }
    public decimal? OverallScore { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<MatchingCriterion> Criteria => _criteria.AsReadOnly();

    public MatchingResult(Guid candidateProfileId, Guid jobId)
    {
        if (candidateProfileId == Guid.Empty)
            throw new ArgumentException(
                "Candidate profile ID is required.",
                nameof(candidateProfileId));

        if (jobId == Guid.Empty)
            throw new ArgumentException(
                "Job ID is required.",
                nameof(jobId));

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

        var totalWeight = evaluatedCriteria.Sum(x => x.Weight);

        _criteria.AddRange(items);

        if (totalWeight == 0)
        {
            OverallScore = null;
            Status = MatchingStatus.InsufficientData;
            CompletedAtUtc = DateTime.UtcNow;
            return;
        }

        var weightedScore = evaluatedCriteria.Sum(
            x => x.Score!.Value * x.Weight) / totalWeight;

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
