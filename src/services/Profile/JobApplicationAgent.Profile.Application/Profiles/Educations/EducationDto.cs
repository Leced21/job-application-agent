namespace JobApplicationAgent.Profile.Application.Profiles.Educations;

public sealed record EducationDto(
    Guid Id,
    string InstitutionName,
    string Degree,
    string? FieldOfStudy,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);