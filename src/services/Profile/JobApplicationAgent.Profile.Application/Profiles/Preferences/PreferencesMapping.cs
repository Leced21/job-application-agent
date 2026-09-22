using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences;

internal static class PreferencesMapping
{
    public static PreferencesDto ToDto(this CandidatePreferences preferences) =>
        new(preferences.CandidateProfileId,
            preferences.DesiredJobTitles.ToArray(),
            preferences.PreferredLocations.ToArray(),
            preferences.ContractTypes.ToArray(),
            preferences.WorkModes.ToArray(),
            preferences.MinimumAnnualGrossSalary,
            preferences.SalaryCurrency,
            preferences.AvailableFrom,
            preferences.CreatedAtUtc,
            preferences.UpdatedAtUtc);
}
