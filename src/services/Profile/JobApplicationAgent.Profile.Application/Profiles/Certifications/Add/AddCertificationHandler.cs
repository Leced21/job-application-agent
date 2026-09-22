using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;

public sealed class AddCertificationHandler(ICandidateProfileRepository repository, IValidator<AddCertificationCommand> validator)
{
    public async Task<CertificationDto> HandleAsync(
        AddCertificationCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(
            command,
            cancellationToken);

        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
            throw new CandidateProfileNotFoundException();

        var certification = profile.AddCertification(
            command.Name,
            command.IssuingOrganization,
            command.IssueDate,
            command.ExpirationDate,
            command.CredentialId,
            command.CredentialUrl);

        await repository.SaveChangesAsync(cancellationToken);

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