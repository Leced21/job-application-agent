namespace JobApplicationAgent.Profile.Application.Profiles.Skills;

public sealed record SkillDto(
    Guid Id,
    string Name,
    string? Category,
    string? Level,
    int? YearsOfExperience,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);