
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Profile.Application.Profiles.Matching;
using JobApplicationAgent.Profile.IntegrationTests.Fixtures;

namespace JobApplicationAgent.Profile.IntegrationTests.Api;

public sealed class ProfileMatchingControllerTests : IClassFixture<ProfileApiFactory>
{
    private const string MatchingRoute = "/api/v1/profile/matching-data";

    private readonly ProfileApiFactory _factory;
    private readonly HttpClient _client;

    public ProfileMatchingControllerTests(ProfileApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMatchingData_ShouldReturnNotFound_WhenProfileDoesNotExist()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Act
        var response = await _client.GetAsync(MatchingRoute);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMatchingData_ShouldReturnProfessionalData_WithoutPersonalContactInformation()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        var profileResponse = await _client.PostAsJsonAsync(
            "/api/v1/profile",
            new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "private@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Développement de pipelines de données."
            });

        Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);

        var experienceResponse = await _client.PostAsJsonAsync(
            "/api/v1/profile/experiences",
            new
            {
                companyName = "Data Company",
                jobTitle = "Data Engineer",
                startDate = new DateOnly(2024, 1, 1),
                endDate = (DateOnly?)null,
                isCurrent = true,
                location = "Paris",
                description = "Développement de pipelines Spark et Databricks."
            });

        Assert.Equal(HttpStatusCode.Created, experienceResponse.StatusCode);

        var skillResponse = await _client.PostAsJsonAsync(
            "/api/v1/profile/skills",
            new
            {
                name = "Python",
                category = "Programming",
                level = "Advanced",
                yearsOfExperience = 4
            });

        Assert.Equal(HttpStatusCode.Created, skillResponse.StatusCode);

        var preferencesResponse = await _client.PostAsJsonAsync(
            "/api/v1/profile/preferences",
            new
            {
                desiredJobTitles = new[] { "Data Engineer" },
                preferredLocations = new[] { "Paris" },
                contractTypes = new[] { "CDI" },
                workModes = new[] { "Hybrid" },
                minimumAnnualGrossSalary = 55000m,
                salaryCurrency = "EUR",
                availableFrom = new DateOnly(2026, 11, 1)
            });

        Assert.Equal(HttpStatusCode.Created, preferencesResponse.StatusCode);

        // Act
        var response = await _client.GetAsync(MatchingRoute);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<ProfileMatchingDataDto>();

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.CandidateProfileId);
        Assert.Equal("Data Engineer", result.JobTitle);

        var experience = Assert.Single(result.Experiences);
        Assert.Equal("Data Company", experience.CompanyName);
        Assert.Equal(
            "Développement de pipelines Spark et Databricks.",
            experience.Description);

        var skill = Assert.Single(result.Skills);
        Assert.Equal("Python", skill.Name);
        Assert.Equal("Advanced", skill.Level);
        Assert.Equal(4, skill.YearsOfExperience);

        Assert.NotNull(result.Preferences);
        Assert.Equal(
            new[] { "Data Engineer" },
            result.Preferences.DesiredJobTitles);
        Assert.Equal(
            new[] { "Paris" },
            result.Preferences.PreferredLocations);
        Assert.Equal(55000m, result.Preferences.MinimumAnnualGrossSalary);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("email", out _));
        Assert.False(root.TryGetProperty("phoneNumber", out _));
        Assert.False(root.TryGetProperty("firstName", out _));
        Assert.False(root.TryGetProperty("lastName", out _));

        Assert.DoesNotContain("private@example.com", json);
        Assert.DoesNotContain("0612345678", json);
    }
}
