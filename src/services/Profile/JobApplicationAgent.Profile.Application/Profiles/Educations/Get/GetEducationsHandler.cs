using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;


namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Get;

public sealed class GetEducationsHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<EducationDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetWithEducationsAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        return profile.Educations
            .OrderByDescending(x => x.StartDate)
            .Select(x => new EducationDto(
                x.Id,
                x.InstitutionName,
                x.Degree,
                x.FieldOfStudy,
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