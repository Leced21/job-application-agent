using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Links.Update;

public sealed class UpdateLinkHandler(ICandidateProfileRepository repository, IValidator<UpdateLinkCommand> validator)
{
    public async Task<LinkDto> HandleAsync(Guid linkId, UpdateLinkCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var link = profile.UpdateLink(
            linkId,
            command.Name,
            command.Url);

        if (link is null)
            throw new LinkNotFoundException(
                linkId);

        await repository.SaveChangesAsync(
            cancellationToken);

        return new LinkDto(
            link.Id,
            link.Name,
            link.Url,
            link.CreatedAtUtc,
            link.UpdatedAtUtc);
    }
}