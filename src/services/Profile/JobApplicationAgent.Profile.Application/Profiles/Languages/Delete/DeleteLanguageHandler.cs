using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Delete;

public sealed class DeleteLanguageHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid languageId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var removed =
            profile.RemoveLanguage(languageId);

        if (!removed)
            throw new LanguageNotFoundException(languageId);

        await repository.SaveChangesAsync(
            cancellationToken);
    }
}