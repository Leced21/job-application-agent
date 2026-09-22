using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Add;

public sealed class AddSkillHandler(
    ICandidateProfileRepository repository,
    IValidator<AddSkillCommand> validator)
{
    public async Task<SkillDto> HandleAsync(
        AddSkillCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var profile = await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        var skill = profile.AddSkill(
            command.Name,
            command.Category,
            command.Level,
            command.YearsOfExperience);

        await repository.SaveChangesAsync(cancellationToken);

        return new SkillDto(
            skill.Id,
            skill.Name,
            skill.Category,
            skill.Level,
            skill.YearsOfExperience,
            skill.CreatedAtUtc,
            skill.UpdatedAtUtc);
    }
}
