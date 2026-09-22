namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Update;

public sealed record UpdateProfessionalExperienceCommand(
    string CompanyName,
    string JobTitle,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description
);