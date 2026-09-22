namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Add;

public sealed record AddSkillCommand(
    string Name,
    string? Category,
    string? Level,
    int? YearsOfExperience);