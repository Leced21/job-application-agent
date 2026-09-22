using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Add;

public sealed class AddLanguageCommandValidator
    : AbstractValidator<AddLanguageCommand>
{
    public AddLanguageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ProficiencyLevel)
            .NotEmpty()
            .MaximumLength(50);
    }
}