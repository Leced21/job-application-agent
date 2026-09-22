using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Delete;

public sealed class DeletePreferencesHandler(ICandidateProfileRepository repository)
{
    public async Task HandleAsync(CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetForUpdateAsync(cancellationToken)
            ?? throw new CandidateProfileNotFoundException();
        if (!profile.RemovePreferences())
            throw new PreferencesNotFoundException();

        await repository.SaveChangesAsync(cancellationToken);
    }
}
