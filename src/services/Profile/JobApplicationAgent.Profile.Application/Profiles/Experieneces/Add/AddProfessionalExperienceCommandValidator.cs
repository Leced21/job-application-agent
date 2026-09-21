using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;

public sealed class AddProfessionalExperienceCommandValidator
    : AbstractValidator<AddProfessionalExperienceCommand>
{
    public AddProfessionalExperienceCommandValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.JobTitle)
            .NotEmpty()
            .MaximumLength(150);

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
                "EndDate must be null for a current professional experience.");
    }
}