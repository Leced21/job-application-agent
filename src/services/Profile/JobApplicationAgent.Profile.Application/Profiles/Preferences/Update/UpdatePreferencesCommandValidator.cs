using FluentValidation;

namespace JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;

public sealed class UpdatePreferencesCommandValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesCommandValidator()
    {
        RuleFor(x => x.DesiredJobTitles).NotNull()
            .Must(items => items is null || items.Length <= 20);
        RuleForEach(x => x.DesiredJobTitles).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PreferredLocations).NotNull()
            .Must(items => items is null || items.Length <= 20);
        RuleForEach(x => x.PreferredLocations).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContractTypes).NotNull()
            .Must(items => items is null || items.Length <= 20);
        RuleForEach(x => x.ContractTypes).NotEmpty().MaximumLength(100);
        RuleFor(x => x.WorkModes).NotNull()
            .Must(items => items is null || items.Length <= 3);
        RuleForEach(x => x.WorkModes)
            .Must(mode => mode is "OnSite" or "Hybrid" or "Remote")
            .WithMessage("Work mode must be OnSite, Hybrid or Remote.");
        RuleFor(x => x.MinimumAnnualGrossSalary)
            .InclusiveBetween(0m, 9999999999.99m)
            .PrecisionScale(12, 2, true);
        RuleFor(x => x.SalaryCurrency)
            .NotEmpty().Matches("^[A-Z]{3}$")
            .When(x => x.MinimumAnnualGrossSalary.HasValue);
        RuleFor(x => x.SalaryCurrency).Null()
            .When(x => !x.MinimumAnnualGrossSalary.HasValue);
    }
}
