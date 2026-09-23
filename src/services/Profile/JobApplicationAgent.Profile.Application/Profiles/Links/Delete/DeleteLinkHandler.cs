using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Links.Delete;

public sealed class DeleteLinkHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid linkId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var removed =
            profile.RemoveLink(linkId);

        if (!removed)
            throw new LinkNotFoundException(linkId);

        await repository.SaveChangesAsync(
            cancellationToken);
    }
}