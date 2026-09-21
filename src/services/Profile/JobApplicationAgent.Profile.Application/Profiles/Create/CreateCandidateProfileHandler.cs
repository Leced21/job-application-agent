using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.Application.Profiles.Create;

public sealed class CreateCandidateProfileHandler(ICandidateProfileRepository repository, IValidator<CreateCandidateProfileCommand> validator)
{
    public async Task<CandidateProfileDto> HandleAsync(CreateCandidateProfileCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var existingProfile = await repository.GetAsync(cancellationToken);

        if (existingProfile is not null)
        {
            throw new CandidateProfileAlreadyExistsException();
        }

        var profile = new CandidateProfile(
            command.FirstName,
            command.LastName,
            command.Email,
            command.PhoneNumber,
            command.JobTitle,
            command.Summary
        );

        await repository.AddAsync(profile, cancellationToken);

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