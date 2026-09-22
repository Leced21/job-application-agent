using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Get;

public sealed class GetCertificationsHandler(
    ICandidateProfileRepository repository)
{
    public async Task<IReadOnlyCollection<CertificationDto>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetWithCertificationsAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        return profile.Certifications
            .OrderBy(x => x.Name)
            .Select(x => new CertificationDto(
                x.Id,
                x.Name,
                x.IssuingOrganization,
                x.IssueDate,
                x.ExpirationDate,
                x.CredentialId,
                x.CredentialUrl,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();
    }
}