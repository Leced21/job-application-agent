using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;

public sealed class AddCertificationCommandValidator : AbstractValidator<AddCertificationCommand>
{
    public AddCertificationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IssuingOrganization).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.ExpirationDate)
            .Must((command, date) => !date.HasValue || date.Value >= command.IssueDate)
            .WithMessage("Expiration date must be on or after the issue date.");
        RuleFor(x => x.CredentialId).MaximumLength(200);
        RuleFor(x => x.CredentialUrl).MaximumLength(2000)
            .Must(url => string.IsNullOrEmpty(url) ||
                (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                 (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)))
            .WithMessage("Credential URL must be an absolute HTTP or HTTPS URL.");
    }
}
