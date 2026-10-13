
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Profiles.Matching;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Matching
{
    public sealed class GetProfileMatchingDataHandlerTests
    {
        private readonly ICandidateProfileRepository _repository;
        private readonly GetProfileMatchingDataHandler _handler;

        public GetProfileMatchingDataHandlerTests()
        {
            _repository = Substitute.For<ICandidateProfileRepository>();
            _handler = new GetProfileMatchingDataHandler(_repository);
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnNull_WhenProfileDoesNotExist()
        {
            // Arrange
            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns((CandidateProfile?)null);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.Null(result);

            await _repository
                .Received(1)
                .GetForMatchingAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnMatchingData_WhenProfileExists()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                "0612345678",
                "Data Engineer",
                "Développement de pipelines de données avec Spark.");

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);

            Assert.Equal(profile.Id, result.CandidateProfileId);
            Assert.Equal(profile.JobTitle, result.JobTitle);
            Assert.Equal(profile.Summary, result.Summary);

            await _repository
                .Received(1)
                .GetForMatchingAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnEmptyCollections_WhenProfileHasNoDetails()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Profil de test");

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);

            Assert.Empty(result.Experiences);
            Assert.Empty(result.Skills);
            Assert.Empty(result.Educations);
            Assert.Empty(result.Certifications);
            Assert.Empty(result.Languages);
        }

        [Fact]
        public async Task HandleAsync_ShouldNotExposePersonalContactInformation()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "private@example.com",
                "0612345678",
                "Data Engineer",
                "Profil professionnel");

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);

            var json = System.Text.Json.JsonSerializer.Serialize(result);

            Assert.DoesNotContain(
                "private@example.com",
                json,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("0612345678", json);

            Assert.DoesNotContain(
                "\"Email\"",
                json,
                StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain(
                "\"PhoneNumber\"",
                json,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task HandleAsync_ShouldMapProfessionalExperiences_WhenExperiencesExist()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Profil Data Engineer");

            profile.AddProfessionalExperience(
                companyName: "Data Company",
                jobTitle: "Data Engineer",
                startDate: new DateOnly(2024, 1, 1),
                endDate: null,
                isCurrent: true,
                location: "Paris",
                description: "Développement de pipelines Spark et Databricks.");

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);

            var experience = Assert.Single(result.Experiences);

            Assert.Equal("Data Company", experience.CompanyName);
            Assert.Equal("Data Engineer", experience.JobTitle);
            Assert.Equal(new DateOnly(2024, 1, 1), experience.StartDate);
            Assert.Null(experience.EndDate);
            Assert.True(experience.IsCurrent);
            Assert.Equal("Paris", experience.Location);
            Assert.Equal(
                "Développement de pipelines Spark et Databricks.",
                experience.Description);
        }

        [Fact]
        public async Task HandleAsync_ShouldMapSkills_WhenSkillsExist()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Profil Data Engineer");

            profile.AddSkill(
                name: "Python",
                category: "Programming",
                level: "Advanced",
                yearsOfExperience: 4);

            profile.AddSkill(
                name: "Databricks",
                category: "Data Engineering",
                level: "Intermediate",
                yearsOfExperience: 2);

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Skills.Count);

            var python = Assert.Single(
                result.Skills,
                skill => skill.Name == "Python");

            Assert.Equal("Programming", python.Category);
            Assert.Equal("Advanced", python.Level);
            Assert.Equal(4, python.YearsOfExperience);

            var databricks = Assert.Single(
                result.Skills,
                skill => skill.Name == "Databricks");

            Assert.Equal("Data Engineering", databricks.Category);
            Assert.Equal("Intermediate", databricks.Level);
            Assert.Equal(2, databricks.YearsOfExperience);
        }

        [Fact]
        public async Task HandleAsync_ShouldMapPreferences_WhenPreferencesExist()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Profil Data Engineer");

            profile.AddPreferences(
                desiredJobTitles: ["Data Engineer", "Data Platform Engineer"],
                preferredLocations: ["Paris", "Lyon"],
                contractTypes: ["CDI"],
                workModes: ["Remote", "Hybrid"],
                minimumAnnualGrossSalary: 55000m,
                salaryCurrency: "EUR",
                availableFrom: new DateOnly(2026, 11, 1));

            _repository
                .GetForMatchingAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Preferences);

            Assert.Equal(
                new[] { "Data Engineer", "Data Platform Engineer" },
                result.Preferences.DesiredJobTitles);

            Assert.Equal(
                new[] { "Paris", "Lyon" },
                result.Preferences.PreferredLocations);

            Assert.Equal(
                new[] { "CDI" },
                result.Preferences.ContractTypes);

            Assert.Equal(
                new[] { "Remote", "Hybrid" },
                result.Preferences.WorkModes);

            Assert.Equal(
                55000m,
                result.Preferences.MinimumAnnualGrossSalary);

            Assert.Equal(
                "EUR",
                result.Preferences.SalaryCurrency);

            Assert.Equal(
                new DateOnly(2026, 11, 1),
                result.Preferences.AvailableFrom);
        }

    }
}