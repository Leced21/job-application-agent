namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Add;

public sealed record AddEducationCommand(
    string InstitutionName,
    string Degree,
    string? FieldOfStudy,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description);