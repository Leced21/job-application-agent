using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Delete;

public sealed class DeleteCertificationHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid certificationId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var removed =
            profile.RemoveCertification(certificationId);

        if (!removed)
            throw new CertificationNotFoundException(certificationId);

        await repository.SaveChangesAsync(
            cancellationToken);
    }
}