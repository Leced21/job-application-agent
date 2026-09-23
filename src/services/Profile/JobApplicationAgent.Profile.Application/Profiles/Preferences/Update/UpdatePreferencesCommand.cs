namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;

public sealed record UpdatePreferencesCommand(
    string[] DesiredJobTitles,
    string[] PreferredLocations,
    string[] ContractTypes,
    string[] WorkModes,
    decimal? MinimumAnnualGrossSalary,
    string? SalaryCurrency,
    DateOnly? AvailableFrom);
