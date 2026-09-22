using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Get;

public sealed class GetPreferencesHandler(ICandidateProfileRepository repository)
{
    public async Task<PreferencesDto> HandleAsync(CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetWithPreferencesAsync(cancellationToken)
            ?? throw new CandidateProfileNotFoundException();
        var preferences = profile.Preferences ?? throw new PreferencesNotFoundException();
        return preferences.ToDto();
    }
}
