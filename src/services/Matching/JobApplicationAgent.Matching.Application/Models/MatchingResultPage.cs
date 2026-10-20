
namespace JobApplicationAgent.Matching.Application.Models;

public sealed record MatchingResultSummary(
    Guid Id,
    Guid CandidateProfileId,
    Guid JobId,
    string Status,
    decimal? OverallScore,
    decimal EvaluationCoverage,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc)
{
    public string ScoringVersion { get; init; } = "legacy-v1";
}

public sealed record MatchingResultPage(
    IReadOnlyCollection<MatchingResultSummary> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
