using FluentValidation;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed class ImportOfferCommandValidator : AbstractValidator<ImportOfferCommand>
{
    public ImportOfferCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(10_000);
        RuleFor(x => x.SourceName).MaximumLength(100);
        RuleFor(x => x.SourceUrl).MaximumLength(2000).Must(OfferNormalizer.IsSupportedUrl)
            .WithMessage("SourceUrl must be an HTTP(S) URL without embedded credentials, at most 2000 characters after normalization.");
        RuleFor(x => x.WorkMode).MaximumLength(50).Must(OfferNormalizer.IsWorkMode).WithMessage("Unknown work mode.");
        RuleFor(x => x.ContractType).MaximumLength(50).Must(OfferNormalizer.IsContract).WithMessage("Unknown contract type.");
        RuleFor(x => x.SalaryMin).InclusiveBetween(0m, 9999999999.99m).PrecisionScale(12, 2, true).When(x => x.SalaryMin.HasValue);
        RuleFor(x => x.SalaryMax).InclusiveBetween(0m, 9999999999.99m).PrecisionScale(12, 2, true).When(x => x.SalaryMax.HasValue);
        RuleFor(x => x).Must(x => !x.SalaryMin.HasValue || !x.SalaryMax.HasValue || x.SalaryMax >= x.SalaryMin)
            .WithMessage("SalaryMax must be greater than or equal to SalaryMin.");
        RuleFor(x => x.SalaryCurrency).MaximumLength(16);
        RuleFor(x => x.SalaryCurrency).Must(value => value is not null &&
            RegexCurrency(OfferNormalizer.Text(value)?.ToUpperInvariant())).When(x => x.SalaryMin.HasValue || x.SalaryMax.HasValue)
            .WithMessage("A three-letter salary currency is required.");
        RuleFor(x => x.SalaryCurrency).Must(value => OfferNormalizer.Text(value) is null || RegexCurrency(value!.Trim().ToUpperInvariant()));
        RuleFor(x => x.PublishedAtUtc).Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc).WithMessage("PublishedAtUtc must be UTC.");
        RuleFor(x => x.ExpiresAtUtc).Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc).WithMessage("ExpiresAtUtc must be UTC.");
        RuleFor(x => x).Must(x => !x.PublishedAtUtc.HasValue || !x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc >= x.PublishedAtUtc)
            .WithMessage("ExpiresAtUtc must not precede PublishedAtUtc.");
        RuleFor(x => x.Skills).Must(skills => skills is null || skills.Length <= 100).WithMessage("At most 100 skills are supported.");
        RuleForEach(x => x.Skills).NotEmpty().MaximumLength(100);
    }
    private static bool RegexCurrency(string? value) => value is { Length: 3 } && value.All(character => character is >= 'A' and <= 'Z');
}
