namespace JobApplicationAgent.Profile.Application.Profiles.Preferences;

public sealed record PreferencesDto(
    Guid CandidateProfileId,
    string[] DesiredJobTitles,
    string[] PreferredLocations,
    string[] ContractTypes,
    string[] WorkModes,
    decimal? MinimumAnnualGrossSalary,
    string? SalaryCurrency,
    DateOnly? AvailableFrom,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
