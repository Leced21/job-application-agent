using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Get;

public sealed class GetLanguagesHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<LanguageDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetWithLanguagesAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        return profile.Languages
            .OrderBy(x => x.Name)
            .Select(x => new LanguageDto(
                x.Id,
                x.Name,
                x.ProficiencyLevel,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}