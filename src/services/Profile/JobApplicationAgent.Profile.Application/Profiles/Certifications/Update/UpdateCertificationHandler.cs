using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Update;

public sealed class UpdateCertificationHandler(ICandidateProfileRepository repository, IValidator<UpdateCertificationCommand> validator)
{
    public async Task<CertificationDto> HandleAsync(Guid certificationId, UpdateCertificationCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(
                cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var certification = profile.UpdateCertification(
            certificationId,
            command.Name,
            command.IssuingOrganization,
            command.IssueDate,
            command.ExpirationDate,
            command.CredentialId,
            command.CredentialUrl);

        if (certification is null)
            throw new CertificationNotFoundException(
                certificationId);

        await repository.SaveChangesAsync(
            cancellationToken);

        return new CertificationDto(
            certification.Id,
            certification.Name,
            certification.IssuingOrganization,
            certification.IssueDate,
            certification.ExpirationDate,
            certification.CredentialId,
            certification.CredentialUrl,
            certification.CreatedAtUtc,
            certification.UpdatedAtUtc);
    }
}