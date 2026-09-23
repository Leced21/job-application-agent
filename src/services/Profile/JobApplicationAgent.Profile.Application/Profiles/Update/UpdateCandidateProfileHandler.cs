using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.Application.Profiles.Update;

public sealed class UpdateCandidateProfileHandler(ICandidateProfileRepository repository, IValidator<UpdateCandidateProfileCommand> validator)
{
    public async Task<CandidateProfileDto> HandleAsync(UpdateCandidateProfileCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var profile = await repository.GetForUpdateAsync(cancellationToken)
            ?? throw new CandidateProfileNotFoundException();

        profile.Update(
            command.FirstName,
            command.LastName,
            command.Email,
            command.PhoneNumber,
            command.JobTitle,
            command.Summary);

        await repository.SaveChangesAsync(cancellationToken);

        return new CandidateProfileDto(
            profile.Id,
            profile.FirstName,
            profile.LastName,
            profile.Email,
            profile.PhoneNumber,
            profile.JobTitle,
            profile.Summary,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc
        );
    }
}