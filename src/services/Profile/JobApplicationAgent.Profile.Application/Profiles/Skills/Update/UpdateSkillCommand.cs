namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Update;

public sealed record UpdateSkillCommand(
    string Name,
    string? Category,
    string? Level,
    int? YearsOfExperience);