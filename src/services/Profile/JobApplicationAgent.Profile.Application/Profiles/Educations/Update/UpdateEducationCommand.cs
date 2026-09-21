namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Update;

public sealed record UpdateEducationCommand(
    string InstitutionName,
    string Degree,
    string? FieldOfStudy,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description);