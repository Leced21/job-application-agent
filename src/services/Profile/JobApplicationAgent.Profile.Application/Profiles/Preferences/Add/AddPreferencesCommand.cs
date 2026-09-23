namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Add;

public sealed record AddPreferencesCommand(
    string[] DesiredJobTitles,
    string[] PreferredLocations,
    string[] ContractTypes,
    string[] WorkModes,
    decimal? MinimumAnnualGrossSalary,
    string? SalaryCurrency,
    DateOnly? AvailableFrom);
