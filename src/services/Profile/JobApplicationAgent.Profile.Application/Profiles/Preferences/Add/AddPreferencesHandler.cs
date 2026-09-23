using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Add;

public sealed class AddPreferencesHandler(
    ICandidateProfileRepository repository,
    IValidator<AddPreferencesCommand> validator)
{
    public async Task<PreferencesDto> HandleAsync(AddPreferencesCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var profile = await repository.GetForUpdateAsync(cancellationToken)
            ?? throw new CandidateProfileNotFoundException();

        if (profile.Preferences is not null)
            throw new PreferencesAlreadyExistsException();

        var preferences = profile.AddPreferences(
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
