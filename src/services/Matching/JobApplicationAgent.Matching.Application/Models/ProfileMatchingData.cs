namespace JobApplicationAgent.Matching.Application.Models;

public sealed record ProfileMatchingData(
    Guid CandidateProfileId,
    string? JobTitle,
    string? Summary,
    IReadOnlyCollection<MatchingExperienceData> Experiences,
    IReadOnlyCollection<MatchingSkillData> Skills,
    IReadOnlyCollection<MatchingEducationData> Educations,
    IReadOnlyCollection<MatchingCertificationData> Certifications,
    IReadOnlyCollection<MatchingLanguageData> Languages,
    MatchingPreferencesData? Preferences);

public sealed record MatchingExperienceData(
    string CompanyName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent,
    string? Location,
    string? Description);

public sealed record MatchingSkillData(
    string Name,
    string? Category,
    string? Level,
    int? YearsOfExperience);

public sealed record MatchingEducationData(
    string InstitutionName,
    string Degree,
    string? FieldOfStudy,
    string? Description);

public sealed record MatchingCertificationData(
    string Name,
    string IssuingOrganization);

public sealed record MatchingLanguageData(
    string Name,
    string ProficiencyLevel);

public sealed record MatchingPreferencesData(
    string[] DesiredJobTitles,
    string[] PreferredLocations,
    string[] ContractTypes,
    string[] WorkModes,
    decimal? MinimumAnnualGrossSalary,
    string? SalaryCurrency,
    DateOnly? AvailableFrom);
