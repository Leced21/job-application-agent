namespace JobApplicationAgent.Profile.Application.Profiles.Languages;

public sealed record LanguageDto(
    Guid Id,
    string Name,
    string ProficiencyLevel,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);