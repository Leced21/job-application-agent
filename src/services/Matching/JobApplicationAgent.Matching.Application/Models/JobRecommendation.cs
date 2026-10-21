using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Models;

public sealed record JobRecommendation(
    Guid JobId,
    string Title,
    string CompanyName,
    string? Location,
    string WorkMode,
    string ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    decimal? OverallScore,
    decimal EvaluationCoverage,
    string Status,
    IReadOnlyCollection<MatchingCriterion> Criteria)
{
    public decimal? RankingScore { get; init; }
    public string ScoringVersion { get; init; } = "legacy-v1";
}
