namespace JobApplicationAgent.Profile.Application.Profiles.Experiences;

public sealed record ProfessionalExperienceDto(
    Guid Id,
    string CompanyName,
    string JobTitle,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);