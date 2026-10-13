namespace JobApplicationAgent.Profile.Application.Profiles.Matching;

public sealed record ProfileMatchingDataDto(
    Guid CandidateProfileId,
    string? JobTitle,
    string? Summary,
    IReadOnlyCollection<MatchingExperienceDto> Experiences,
    IReadOnlyCollection<MatchingSkillDto> Skills,
    IReadOnlyCollection<MatchingEducationDto> Educations,
    IReadOnlyCollection<MatchingCertificationDto> Certifications,
    IReadOnlyCollection<MatchingLanguageDto> Languages,
    MatchingPreferencesDto? Preferences
);

public sealed record MatchingExperienceDto(
    string CompanyName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Location,
    string? Description
);

public sealed record MatchingSkillDto(
    string Name,
    string? Category,
    string? Level,
    int? YearsOfExperience
);

public sealed record MatchingEducationDto(
    string InstitutionName,
    string Degree,
    string? FieldOfStudy,
    string? Description
);

public sealed record MatchingCertificationDto(
    string Name,
    string IssuingOrganization
);

public sealed record MatchingLanguageDto(
    string Name,
    string ProficiencyLevel
);

public sealed record MatchingPreferencesDto(
    string[] DesiredJobTitles,
    string[] PreferredLocations,
    string[] ContractTypes,
    string[] WorkModes,
    decimal? MinimumAnnualGrossSalary,
    string? SalaryCurrency,
    DateOnly? AvailableFrom
);