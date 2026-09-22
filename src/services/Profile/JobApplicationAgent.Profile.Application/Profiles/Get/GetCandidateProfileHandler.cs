using JobApplicationAgent.Profile.Application.Abstractions;

namespace JobApplicationAgent.Profile.Application.Profiles.Get;

public sealed class GetCandidateProfileHandler(ICandidateProfileRepository repository)
{
    public async Task<CandidateProfileDto?> HandleAsync(CancellationToken cancellationToken = default)
    {
        var profile = await repository.GetAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

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