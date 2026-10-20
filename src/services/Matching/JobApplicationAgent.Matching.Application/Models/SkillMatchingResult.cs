namespace JobApplicationAgent.Matching.Application.Models;

public sealed record SkillMatchingResult(
    decimal? Score,
    IReadOnlyCollection<string> RequiredSkills,
    IReadOnlyCollection<string> MatchedSkills,
    IReadOnlyCollection<string> MissingSkills);
