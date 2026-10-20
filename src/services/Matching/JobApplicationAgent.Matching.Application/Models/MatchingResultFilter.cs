
namespace JobApplicationAgent.Matching.Application.Models;

public sealed record MatchingResultFilter(
    Guid? CandidateProfileId,
    Guid? JobId,
    string? Status,
    int Page = 1,
    int PageSize = 20);
