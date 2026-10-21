namespace JobApplicationAgent.Matching.Application.Models;

public sealed record RecommendationResponse(
    Guid CandidateProfileId,
    IReadOnlyList<JobRecommendation> Items,
    int Page,
    int PageSize)
{
    public int TotalCount { get; init; }
    public string ScoringVersion { get; init; } = "legacy-v1";
    public int TotalPages => TotalCount == 0 ? 0 : (TotalCount - 1) / PageSize + 1;
}
