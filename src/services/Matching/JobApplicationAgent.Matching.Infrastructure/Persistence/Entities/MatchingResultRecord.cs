namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;

public sealed class MatchingResultRecord
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public Guid JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ScoringVersion { get; set; } = "legacy-v1";
    public decimal? OverallScore { get; set; }
    public decimal EvaluationCoverage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public List<MatchingCriterionRecord> Criteria { get; set; } = [];
}
