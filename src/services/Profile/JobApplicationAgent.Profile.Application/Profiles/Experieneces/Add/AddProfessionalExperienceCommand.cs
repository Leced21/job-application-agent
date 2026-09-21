namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;

public sealed record AddProfessionalExperienceCommand(
    string CompanyName,
    string JobTitle,
    string? Location,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Description
);