using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Add;

public sealed class AddEducationCommandValidator
    : AbstractValidator<AddEducationCommand>
{
    public AddEducationCommandValidator()
    {
        RuleFor(x => x.InstitutionName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Degree)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.FieldOfStudy)
            .MaximumLength(200)
            .When(x => x.FieldOfStudy is not null);

        RuleFor(x => x.Location)
            .MaximumLength(200)
            .When(x => x.Location is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000)
            .When(x => x.Description is not null);

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.EndDate)
            .Null()
            .When(x => x.IsCurrent)
            .WithMessage(
                "EndDate must be null for a current education.");
    }
}