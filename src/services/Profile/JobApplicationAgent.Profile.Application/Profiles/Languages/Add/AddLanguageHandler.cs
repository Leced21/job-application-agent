using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Add;

public sealed class AddLanguageHandler(ICandidateProfileRepository repository, IValidator<AddLanguageCommand> validator)
{
    public async Task<LanguageDto> HandleAsync(
        AddLanguageCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var language = profile.AddLanguage(
            command.Name,
            command.ProficiencyLevel);

        await repository.SaveChangesAsync(cancellationToken);

        return new LanguageDto(
            language.Id,
            language.Name,
            language.ProficiencyLevel,
            language.CreatedAtUtc,
            language.UpdatedAtUtc);
    }
}