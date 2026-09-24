using FluentValidation;

namespace JobApplicationAgent.Job.Application.Jobs.Update;

public sealed class UpdateJobCommandValidator
    : AbstractValidator<UpdateJobCommand>
{
    public UpdateJobCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.CompanyName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Location)
            .MaximumLength(200)
            .When(x => x.Location is not null);

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(0)
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x)
            .Must(x =>
                !x.SalaryMin.HasValue ||
                !x.SalaryMax.HasValue ||
                x.SalaryMax.Value >= x.SalaryMin.Value)
            .WithMessage(
                "SalaryMax must be greater than or equal to SalaryMin.");

        RuleFor(x => x.SalaryCurrency)
            .MaximumLength(3)
            .When(x => x.SalaryCurrency is not null);

        RuleFor(x => x.Description)
            .MaximumLength(10_000)
            .When(x => x.Description is not null);

        RuleFor(x => x.Source)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SourceUrl)
            .MaximumLength(2_000)
            .When(x => x.SourceUrl is not null);
    }
}