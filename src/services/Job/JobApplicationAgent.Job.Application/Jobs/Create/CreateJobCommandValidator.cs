using FluentValidation;

namespace JobApplicationAgent.Job.Application.Jobs.Create;

public sealed class CreateJobCommandValidator
    : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator()
    {
        RuleFor(x => x.WorkMode).IsInEnum();
        RuleFor(x => x.ContractType).IsInEnum();
        RuleFor(x => x.PublishedAtUtc).Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("PublishedAtUtc must be UTC.");
        RuleFor(x => x.SourceUrl).Must(url => string.IsNullOrEmpty(url) ||
            (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            .WithMessage("SourceUrl must be an HTTP or HTTPS URL.");
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
            .InclusiveBetween(0m, 9999999999.99m).PrecisionScale(12, 2, true)
            .When(x => x.SalaryMin.HasValue);

        RuleFor(x => x.SalaryMax)
            .InclusiveBetween(0m, 9999999999.99m).PrecisionScale(12, 2, true)
            .When(x => x.SalaryMax.HasValue);

        RuleFor(x => x)
            .Must(x =>
                !x.SalaryMin.HasValue ||
                !x.SalaryMax.HasValue ||
                x.SalaryMax.Value >= x.SalaryMin.Value)
            .WithMessage(
                "SalaryMax must be greater than or equal to SalaryMin.");

        RuleFor(x => x.SalaryCurrency).NotEmpty().Matches("^[A-Z]{3}$")
            .When(x => x.SalaryMin.HasValue || x.SalaryMax.HasValue);
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