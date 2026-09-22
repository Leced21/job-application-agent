using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;

public sealed class UpdatePreferencesHandler(
    ICandidateProfileRepository repository,
    IValidator<UpdatePreferencesCommand> validator)
{
    public async Task<PreferencesDto> HandleAsync(UpdatePreferencesCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var profile = await repository.GetForUpdateAsync(cancellationToken)
            ?? throw new CandidateProfileNotFoundException();

        var preferences = profile.SetPreferences(
            command.DesiredJobTitles,
            command.PreferredLocations,
            command.ContractTypes,
            command.WorkModes,
            command.MinimumAnnualGrossSalary,
            command.SalaryCurrency,
            command.AvailableFrom);

        await repository.SaveChangesAsync(cancellationToken);
        return preferences.ToDto();
    }
}
