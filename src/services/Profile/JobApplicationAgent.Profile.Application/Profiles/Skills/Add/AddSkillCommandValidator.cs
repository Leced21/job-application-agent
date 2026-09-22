using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Skills.Add;

public sealed class AddSkillCommandValidator
    : AbstractValidator<AddSkillCommand>
{
    public AddSkillCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .When(x => x.Category is not null);

        RuleFor(x => x.Level)
            .MaximumLength(50)
            .When(x => x.Level is not null);

        RuleFor(x => x.YearsOfExperience)
            .GreaterThanOrEqualTo(0)
            .When(x => x.YearsOfExperience.HasValue);
    }
}