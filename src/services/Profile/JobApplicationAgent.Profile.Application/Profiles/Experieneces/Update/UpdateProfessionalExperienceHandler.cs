using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Update;

public sealed class UpdateProfessionalExperienceHandler(
    ICandidateProfileRepository repository,
    IValidator<UpdateProfessionalExperienceCommand> validator)
{
    public async Task<ProfessionalExperienceDto> HandleAsync(
        Guid experienceId,
        UpdateProfessionalExperienceCommand command,
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

        var experience = profile.UpdateProfessionalExperience(
            experienceId,
            command.CompanyName,
            command.JobTitle,
            command.StartDate,
            command.EndDate,
            command.IsCurrent,
            command.Location,
            command.Description);

        if (experience is null)
        {
            throw new ProfessionalExperienceNotFoundException(
                experienceId);
        }

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
            experience.UpdatedAtUtc);
    }
}