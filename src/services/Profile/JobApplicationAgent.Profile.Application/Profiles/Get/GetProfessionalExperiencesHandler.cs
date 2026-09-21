using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Get;

public sealed class GetProfessionalExperiencesHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<ProfessionalExperienceDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetWithProfessionalExperiencesAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        return profile.ProfessionalExperiences
            .OrderByDescending(x => x.StartDate)
            .Select(x => new ProfessionalExperienceDto(
                x.Id,
                x.CompanyName,
                x.JobTitle,
                x.Location,
                x.StartDate,
                x.EndDate,
                x.IsCurrent,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}