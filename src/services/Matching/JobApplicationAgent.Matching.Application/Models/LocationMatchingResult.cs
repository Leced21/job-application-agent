namespace JobApplicationAgent.Matching.Application.Models;

public sealed record LocationMatchingResult(
    decimal? Score,
    string Explanation);
