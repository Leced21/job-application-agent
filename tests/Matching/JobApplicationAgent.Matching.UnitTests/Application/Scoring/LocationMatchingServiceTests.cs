
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Application.Scoring;

namespace JobApplicationAgent.Matching.UnitTests.Scoring;

public sealed class LocationMatchingServiceTests
{
    private readonly LocationMatchingService _service = new();

    private static MatchingPreferencesData Preferences(
        string[] locations,
        string[] workModes)
    {
        return new MatchingPreferencesData(
            DesiredJobTitles: [],
            PreferredLocations: locations,
            ContractTypes: [],
            WorkModes: workModes,
            MinimumAnnualGrossSalary: null,
            SalaryCurrency: null,
            AvailableFrom: null);
    }

    [Fact]
    public void Evaluate_ShouldReturn100_WhenCityAndWorkModeMatch()
    {
        var result = _service.Evaluate(
            Preferences(["Paris"], ["Hybrid"]),
            "Paris",
            "Hybrid");

        Assert.Equal(100m, result.Score);
    }

    [Fact]
    public void Evaluate_ShouldReturn0_WhenCityDoesNotMatch()
    {
        var result = _service.Evaluate(
            Preferences(["Paris"], ["OnSite"]),
            "Lyon",
            "OnSite");

        Assert.Equal(0m, result.Score);
    }

    [Fact]
    public void Evaluate_ShouldReturn100_WhenRemoteIsAccepted()
    {
        var result = _service.Evaluate(
            Preferences(["Paris"], ["Remote"]),
            "Lyon",
            "Remote");

        Assert.Equal(100m, result.Score);
    }

    [Fact]
    public void Evaluate_ShouldReturn0_WhenWorkModeIsRejected()
    {
        var result = _service.Evaluate(
            Preferences(["Paris"], ["Remote"]),
            "Paris",
            "OnSite");

        Assert.Equal(0m, result.Score);
    }

    [Fact]
    public void Evaluate_ShouldReturnNull_WhenPreferencesAreMissing()
    {
        var result = _service.Evaluate(
            null,
            "Paris",
            "Hybrid");

        Assert.Null(result.Score);
    }

    [Fact]
    public void Evaluate_ShouldReturnNull_WhenWorkModePreferencesAreMissing()
    {
        var result = _service.Evaluate(
            Preferences(["Paris"], []),
            "Paris",
            "Hybrid");

        Assert.Null(result.Score);
    }

    [Fact]
    public void Evaluate_ShouldIgnoreCaseAndAccents()
    {
        var result = _service.Evaluate(
            Preferences(["Évry"], ["Hybrid"]),
            "evry",
            "hybrid");

        Assert.Equal(100m, result.Score);
    }
}
