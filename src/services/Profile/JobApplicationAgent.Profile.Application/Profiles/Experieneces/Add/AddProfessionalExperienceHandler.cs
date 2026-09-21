using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;

public sealed class AddProfessionalExperienceHandler(ICandidateProfileRepository repository,IValidator<AddProfessionalExperienceCommand> validator)
{
    public async Task<ProfessionalExperienceDto> HandleAsync(
        AddProfessionalExperienceCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        var experience = profile.AddProfessionalExperience(
            command.CompanyName,
            command.JobTitle,
            command.StartDate,
            command.EndDate,
            command.IsCurrent,
            command.Location,
            command.Description);

        await repository.SaveChangesAsync(cancellationToken);

        return new ProfessionalExperienceDto(
            experience.Id,
            experience.CompanyName,
            experience.JobTitle,
            experience.Location,
            experience.StartDate,
            experience.EndDate,
            experience.IsCurrent,
            experience.Description,
            experience.CreatedAtUtc,
            experience.UpdatedAtUtc
        );
    }
}