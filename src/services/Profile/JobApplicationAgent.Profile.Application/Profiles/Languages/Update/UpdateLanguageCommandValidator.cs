using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Update;

public sealed class UpdateLanguageCommandValidator
    : AbstractValidator<UpdateLanguageCommand>
{
    public UpdateLanguageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ProficiencyLevel)
            .NotEmpty()
            .MaximumLength(50);
    }
}