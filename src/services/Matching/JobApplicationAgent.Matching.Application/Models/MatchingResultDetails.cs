namespace JobApplicationAgent.Matching.Application.Models;

public sealed record MatchingCriterionDetails(
    string Name,
    decimal? Score,
    decimal Weight,
    string Status,
    string Explanation);

public sealed record MatchingResultDetails(
    Guid Id,
    Guid CandidateProfileId,
    Guid JobId,
    string Status,
    decimal? OverallScore,
    decimal EvaluationCoverage,
    IReadOnlyCollection<MatchingCriterionDetails> Criteria,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc)
{
    public string ScoringVersion { get; init; } = "legacy-v1";
}
