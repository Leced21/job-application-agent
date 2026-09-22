using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Get;

public sealed class GetSkillsHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<SkillDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetWithSkillsAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        return profile.Skills
            .OrderBy(x => x.Name)
            .Select(x => new SkillDto(
                x.Id,
                x.Name,
                x.Category,
                x.Level,
                x.YearsOfExperience,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}