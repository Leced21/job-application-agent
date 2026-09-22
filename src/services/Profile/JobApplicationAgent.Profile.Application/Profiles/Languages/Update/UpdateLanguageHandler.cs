using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Update;

public sealed class UpdateLanguageHandler(ICandidateProfileRepository repository, IValidator<UpdateLanguageCommand> validator)
{
    public async Task<LanguageDto> HandleAsync(Guid languageId, UpdateLanguageCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var language = profile.UpdateLanguage(
            languageId,
            command.Name,
            command.ProficiencyLevel);

        if (language is null)
            throw new LanguageNotFoundException(
                languageId);

        await repository.SaveChangesAsync(
            cancellationToken);

        return new LanguageDto(
            language.Id,
            language.Name,
            language.ProficiencyLevel,
            language.CreatedAtUtc,
            language.UpdatedAtUtc);
    }
}