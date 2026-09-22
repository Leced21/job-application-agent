using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Links.Get;

public sealed class GetLinksHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<LinkDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetWithLinksAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        return profile.Links
            .OrderBy(x => x.Name)
            .Select(x => new LinkDto(
                x.Id,
                x.Name,
                x.Url,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}