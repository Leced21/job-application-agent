using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Add;

public sealed class AddEducationHandler(
    ICandidateProfileRepository repository,
    IValidator<AddEducationCommand> validator)
{
    public async Task<EducationDto> HandleAsync(
        AddEducationCommand command,
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

        var education = profile.AddEducation(
            command.InstitutionName,
            command.Degree,
            command.StartDate,
            command.EndDate,
            command.IsCurrent,
            command.FieldOfStudy,
            command.Location,
            command.Description);

        await repository.SaveChangesAsync(cancellationToken);

        return new EducationDto(
            education.Id,
            education.InstitutionName,
            education.Degree,
            education.FieldOfStudy,
            education.Location,
            education.StartDate,
            education.EndDate,
            education.IsCurrent,
            education.Description,
            education.CreatedAtUtc,
            education.UpdatedAtUtc);
    }
}