using JobApplicationAgent.Profile.Application.Profiles.Preferences.Add;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Preferences.Add;

public sealed class AddPreferencesCommandValidatorTests
{
    private readonly AddPreferencesCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldAcceptNoCriteria()
    {
        Assert.True(_validator.Validate(new AddPreferencesCommand([], [], [], [], null, null, null)).IsValid);
    }

    [Theory]
    [InlineData("OnSite")]
    [InlineData("Hybrid")]
    [InlineData("Remote")]
    public void Validate_ShouldAcceptWorkModesAndSalary(string mode)
    {
        Assert.True(_validator.Validate(new AddPreferencesCommand(["Developer"], ["Paris"], ["CDI"], [mode],
            55000.25m, "EUR", new DateOnly(2027, 1, 1))).IsValid);
    }

    [Theory]
    [InlineData("nullTitles")]
    [InlineData("nullLocations")]
    [InlineData("nullContracts")]
    [InlineData("nullModes")]
    [InlineData("emptyTitle")]
    [InlineData("longTitle")]
    [InlineData("tooManyTitles")]
    [InlineData("emptyLocation")]
    [InlineData("longLocation")]
    [InlineData("tooManyLocations")]
    [InlineData("emptyContract")]
    [InlineData("longContract")]
    [InlineData("tooManyContracts")]
    [InlineData("badMode")]
    [InlineData("tooManyModes")]
    [InlineData("negativeSalary")]
    [InlineData("salaryPrecision")]
    [InlineData("salaryOverflow")]
    [InlineData("missingCurrency")]
    [InlineData("badCurrency")]
    [InlineData("currencyWithoutSalary")]
    public void Validate_ShouldRejectInvalidCriteria(string scenario)
    {
        var command = new AddPreferencesCommand([], [], [], [], null, null, null);
        command = scenario switch
        {
            "nullTitles" => command with { DesiredJobTitles = null! },
            "nullLocations" => command with { PreferredLocations = null! },
            "nullContracts" => command with { ContractTypes = null! },
            "nullModes" => command with { WorkModes = null! },
            "emptyTitle" => command with { DesiredJobTitles = [" "] },
            "longTitle" => command with { DesiredJobTitles = [new string('a', 151)] },
            "tooManyTitles" => command with { DesiredJobTitles = Enumerable.Repeat("Developer", 21).ToArray() },
            "emptyLocation" => command with { PreferredLocations = [""] },
            "longLocation" => command with { PreferredLocations = [new string('a', 201)] },
            "tooManyLocations" => command with { PreferredLocations = Enumerable.Repeat("Paris", 21).ToArray() },
            "emptyContract" => command with { ContractTypes = [""] },
            "longContract" => command with { ContractTypes = [new string('a', 101)] },
            "tooManyContracts" => command with { ContractTypes = Enumerable.Repeat("CDI", 21).ToArray() },
            "badMode" => command with { WorkModes = ["Anything"] },
            "tooManyModes" => command with { WorkModes = ["Remote", "Remote", "Remote", "Remote"] },
            "negativeSalary" => command with { MinimumAnnualGrossSalary = -1, SalaryCurrency = "EUR" },
            "salaryPrecision" => command with { MinimumAnnualGrossSalary = 1.001m, SalaryCurrency = "EUR" },
            "salaryOverflow" => command with { MinimumAnnualGrossSalary = 10000000000m, SalaryCurrency = "EUR" },
            "missingCurrency" => command with { MinimumAnnualGrossSalary = 100m },
            "badCurrency" => command with { MinimumAnnualGrossSalary = 100m, SalaryCurrency = "euros" },
            _ => command with { SalaryCurrency = "EUR" }
        };
        Assert.False(_validator.Validate(command).IsValid);
    }
}
