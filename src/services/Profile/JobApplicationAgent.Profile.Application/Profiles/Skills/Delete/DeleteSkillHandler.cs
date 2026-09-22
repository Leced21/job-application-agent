using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Delete;

public sealed class DeleteSkillHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid skillId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        var removed = profile.RemoveSkill(skillId);

        if (!removed)
        {
            throw new SkillNotFoundException(skillId);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}