namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;

public sealed class MatchingCriterionRecord
{
    public Guid Id { get; set; }
    public Guid MatchingResultId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public decimal Weight { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public MatchingResultRecord MatchingResult { get; set; } = null!;
}