using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Update;

public sealed class UpdateSkillHandler(
    ICandidateProfileRepository repository,
    IValidator<UpdateSkillCommand> validator)
{
    public async Task<SkillDto> HandleAsync(
        Guid skillId,
        UpdateSkillCommand command,
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

        var skill = profile.UpdateSkill(
            skillId,
            command.Name,
            command.Category,
            command.Level,
            command.YearsOfExperience);

        if (skill is null)
        {
            throw new SkillNotFoundException(skillId);
        }

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