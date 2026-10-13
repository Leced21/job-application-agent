
using JobApplicationAgent.Profile.Application.Abstractions;

namespace JobApplicationAgent.Profile.Application.Profiles.Matching;

public sealed class GetProfileMatchingDataHandler(
    ICandidateProfileRepository repository)
{
    public async Task<ProfileMatchingDataDto?> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetForMatchingAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        return new ProfileMatchingDataDto(
            profile.Id,
            profile.JobTitle,
            profile.Summary,

            profile.ProfessionalExperiences
                .Select(x => new MatchingExperienceDto(
                    x.CompanyName,
                    x.JobTitle,
                    x.StartDate,
                    x.EndDate,
                    x.IsCurrent,
                    x.Location,
                    x.Description))
                .ToArray(),

            profile.Skills
                .Select(x => new MatchingSkillDto(
                    x.Name,
                    x.Category,
                    x.Level,
                    x.YearsOfExperience))
                .ToArray(),

            profile.Educations
                .Select(x => new MatchingEducationDto(
                    x.InstitutionName,
                    x.Degree,
                    x.FieldOfStudy,
                    x.Description))
                .ToArray(),

            profile.Certifications
                .Select(x => new MatchingCertificationDto(
                    x.Name,
                    x.IssuingOrganization))
                .ToArray(),

            profile.Languages
                .Select(x => new MatchingLanguageDto(
                    x.Name,
                    x.ProficiencyLevel))
                .ToArray(),

            profile.Preferences is null
                ? null
                : new MatchingPreferencesDto(
                    profile.Preferences.DesiredJobTitles,
                    profile.Preferences.PreferredLocations,
                    profile.Preferences.ContractTypes,
                    profile.Preferences.WorkModes,
                    profile.Preferences.MinimumAnnualGrossSalary,
                    profile.Preferences.SalaryCurrency,
                    profile.Preferences.AvailableFrom)
        );
    }
}