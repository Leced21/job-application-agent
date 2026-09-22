using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Links.Add;

public sealed class AddLinkCommandValidator : AbstractValidator<AddLinkCommand>
{
    public AddLinkCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("URL must be an absolute HTTP or HTTPS URL.");
    }
}
