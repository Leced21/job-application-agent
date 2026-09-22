using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Links.Add;

public sealed class AddLinkHandler(ICandidateProfileRepository repository, IValidator<AddLinkCommand> validator)
{
    public async Task<LinkDto> HandleAsync(
        AddLinkCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var link = profile.AddLink(
            command.Name,
            command.Url);

        await repository.SaveChangesAsync(cancellationToken);

        return new LinkDto(
            link.Id,
            link.Name,
            link.Url,
            link.CreatedAtUtc,
            link.UpdatedAtUtc);
    }
}