using JobApplicationAgent.Profile.Application.Profiles.Update;
using JobApplicationAgent.Profile.Application.Profiles.Preferences;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using JobApplicationAgent.Profile.Application.Profiles.Links;
using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Profile.Application.Profiles;
using JobApplicationAgent.Profile.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using JobApplicationAgent.Profile.Application.Profiles.Experiences;
using JobApplicationAgent.Profile.Application.Profiles.Educations;
using JobApplicationAgent.Profile.Application.Profiles.Skills;
using JobApplicationAgent.Profile.Application.Profiles.Languages;

namespace JobApplicationAgent.Profile.IntegrationTests.Api
{
    public sealed class ProfileControllerTests :
    IClassFixture<ProfileApiFactory>, IAsyncLifetime
    {
        private readonly ProfileApiFactory _factory;
        private readonly HttpClient _client;

        public ProfileControllerTests(ProfileApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return _factory.ResetDatabaseAsync();
        }

        [Fact]
        public async Task Navigations_ShouldLoadBothDirectionsWithoutChangingDatabaseSchema()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
            Assert.False(db.Database.HasPendingModelChanges());
            var profile = new JobApplicationAgent.Profile.Domain.Entities.CandidateProfile("Test", "Candidate", "navigations@example.com");
            profile.AddPreferences([], [], [], []);
            profile.AddCertification("Certification", "Issuer", new DateOnly(2025, 1, 1));
            profile.AddEducation("University", "Degree", new DateOnly(2020, 1, 1));
            profile.AddLanguage("French", "Native");
            profile.AddLink("Portfolio", "https://example.com");
            profile.AddProfessionalExperience("Company", "Developer", new DateOnly(2022, 1, 1));
            profile.AddSkill("C#");
            db.CandidateProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var loaded = await db.CandidateProfiles
                .Include(x => x.Preferences).Include(x => x.Certifications)
                .Include(x => x.Educations).Include(x => x.Languages)
                .Include(x => x.Links).Include(x => x.ProfessionalExperiences)
                .Include(x => x.Skills).AsSplitQuery().SingleAsync();
            Assert.Same(loaded, loaded.Preferences!.CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.Certifications).CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.Educations).CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.Languages).CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.Links).CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.ProfessionalExperiences).CandidateProfile);
            Assert.Same(loaded, Assert.Single(loaded.Skills).CandidateProfile);

            db.ChangeTracker.Clear();
            var preferences = await db.Preferences.Include(x => x.CandidateProfile).SingleAsync();
            Assert.Equal(profile.Id, preferences.CandidateProfile.Id);
            Assert.Same(preferences, preferences.CandidateProfile.Preferences);
        }

        [Fact]
        public async Task Get_ShouldReturnNotFound_WhenProfileDoesNotExist()
        {
            // Act
            var response = await _client.GetAsync("/api/v1/profile");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task Create_ShouldReturnCreated_WhenCommandIsValid()
        {
            // Arrange
            var command = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            // Act
            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                command);

            // Assert
            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var profile =
                await response.Content
                    .ReadFromJsonAsync<CandidateProfileDto>();

            Assert.NotNull(profile);
            Assert.NotEqual(Guid.Empty, profile.Id);

            Assert.Equal(
                "Test",
                profile.FirstName);

            Assert.Equal(
                "Candidate",
                profile.LastName);

            Assert.Equal(
                "test@example.com",
                profile.Email);

            Assert.Equal(
                "Data Engineer",
                profile.JobTitle);

            Assert.Equal(
                "Integration test profile",
                profile.Summary);
        }
        [Fact]
        public async Task Create_ShouldReturnBadRequest_WhenCommandIsInvalid()
        {
            // Arrange
            var command = new
            {
                firstName = "",
                lastName = "",
                email = "invalid-email",
                phoneNumber = (string?)null,
                jobTitle = (string?)null,
                summary = (string?)null
            };

            // Act
            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                command);

            // Assert
            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);

            var problem =
                await response.Content
                    .ReadFromJsonAsync<HttpValidationProblemDetails>();

            Assert.NotNull(problem);

            Assert.Equal(
                StatusCodes.Status400BadRequest,
                problem.Status);

            Assert.Equal(
                "Validation failed",
                problem.Title);

            Assert.Contains(
                "FirstName",
                problem.Errors.Keys);

            Assert.Contains(
                "LastName",
                problem.Errors.Keys);

            Assert.Contains(
                "Email",
                problem.Errors.Keys);
        }

        [Fact]
        public async Task Get_ShouldReturnOk_WhenProfileExists()
        {
            // Arrange
            var command = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                command);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            // Act
            var response =
                await _client.GetAsync("/api/v1/profile");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var profile = await response.Content.ReadFromJsonAsync<CandidateProfileDto>();

            Assert.NotNull(profile);
            Assert.Equal("Test", profile.FirstName);
            Assert.Equal("Candidate", profile.LastName);
            Assert.Equal("test@example.com", profile.Email);
            Assert.Equal("0612345678", profile.PhoneNumber);
            Assert.Equal("Data Engineer", profile.JobTitle);
            Assert.Equal("Integration test profile", profile.Summary);
        }

        [Fact]
        public async Task Create_ShouldReturnConflict_WhenProfileAlreadyExists()
        {
            // Arrange
            var firstCommand = new
            {
                firstName = "First",
                lastName = "Candidate",
                email = "first@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "First profile"
            };

            var secondCommand = new
            {
                firstName = "Second",
                lastName = "Candidate",
                email = "second@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Second profile"
            };

            var firstResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                firstCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                firstResponse.StatusCode);

            // Act
            var secondResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                secondCommand);

            // Assert
            Assert.Equal(
                HttpStatusCode.Conflict,
                secondResponse.StatusCode);
            var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>();

            Assert.NotNull(problem);
            Assert.Equal(
                "Candidate profile already exists",
                problem.Title);

            Assert.Equal(
                "A candidate profile already exists.",
                problem.Detail);

            Assert.Equal(
                StatusCodes.Status409Conflict,
                problem.Status);
        }
        [Fact]
        public async Task AddExperience_ShouldReturnCreated_WhenCommandIsValid()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync("/api/v1/profile", profileCommand);

            Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);

            var command = new
            {
                companyName = "Test Company",
                jobTitle = "Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "DÃ©veloppement de pipelines de donnÃ©es."
            };

            var response =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    command);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var experience = await response.Content.ReadFromJsonAsync<ProfessionalExperienceDto>();

            Assert.NotNull(experience);
            Assert.NotEqual(Guid.Empty, experience.Id);
            Assert.Equal("Test Company", experience.CompanyName);
            Assert.Equal("Data Engineer", experience.JobTitle);
            Assert.Equal("Paris", experience.Location);
            Assert.Equal(new DateOnly(2024, 1, 1), experience.StartDate);
            Assert.Null(experience.EndDate);
            Assert.True(experience.IsCurrent);
            Assert.Equal("DÃ©veloppement de pipelines de donnÃ©es.", experience.Description);
        }
        [Fact]
        public async Task AddExperience_ShouldReturnNotFound_WhenProfileDoesNotExist()
        {
            var command = new
            {
                companyName = "Test Company",
                jobTitle = "Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "DÃ©veloppement de pipelines de donnÃ©es."
            };

            var response =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    command);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var problem =
                await response.Content.ReadFromJsonAsync<ProblemDetails>();

            Assert.NotNull(problem);
            Assert.Equal(
                StatusCodes.Status404NotFound,
                problem.Status);

            Assert.Equal(
                "Candidate profile not found",
                problem.Title);

            Assert.Equal(
                "Candidate profile does not exist.",
                problem.Detail);
        }
        [Fact]
        public async Task AddExperience_ShouldReturnBadRequest_WhenCurrentExperienceHasEndDate()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync("/api/v1/profile", profileCommand);

            Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);

            var command = new
            {
                companyName = "Test Company",
                jobTitle = "Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = "2025-01-01",
                isCurrent = true,
                description = "DÃ©veloppement de pipelines de donnÃ©es."
            };

            var response =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    command);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var problem =
                await response.Content
                    .ReadFromJsonAsync<HttpValidationProblemDetails>();

            Assert.NotNull(problem);
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                problem.Status);

            Assert.Contains(
                "EndDate",
                problem.Errors.Keys);
        }
        [Fact]
        public async Task GetExperiences_ShouldReturnExperiences_WhenProfileExists()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var firstExperience = new
            {
                companyName = "First Company",
                jobTitle = "Data Engineer",
                location = "Lyon",
                startDate = "2022-01-01",
                endDate = "2023-12-31",
                isCurrent = false,
                description = "PremiÃ¨re expÃ©rience."
            };

            var secondExperience = new
            {
                companyName = "Current Company",
                jobTitle = "Senior Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "ExpÃ©rience actuelle."
            };

            var firstResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    firstExperience);

            var secondResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    secondExperience);

            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

            var response =
                await _client.GetAsync("/api/v1/profile/experiences");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var experiences =
                await response.Content.ReadFromJsonAsync<
                    List<ProfessionalExperienceDto>>();

            Assert.NotNull(experiences);
            Assert.Equal(2, experiences.Count);

            Assert.Equal("Current Company", experiences[0].CompanyName);
            Assert.Equal(
                new DateOnly(2024, 1, 1),
                experiences[0].StartDate);

            Assert.Equal("First Company", experiences[1].CompanyName);
            Assert.Equal(
                new DateOnly(2022, 1, 1),
                experiences[1].StartDate);
        }
        [Fact]
        public async Task UpdateExperience_ShouldReturnOk_WhenExperienceExists()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var createExperienceCommand = new
            {
                companyName = "Old Company",
                jobTitle = "Data Engineer",
                location = "Lyon",
                startDate = "2022-01-01",
                endDate = "2023-12-31",
                isCurrent = false,
                description = "Old description"
            };

            var createResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    createExperienceCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdExperience =
                await createResponse.Content
                    .ReadFromJsonAsync<ProfessionalExperienceDto>();

            Assert.NotNull(createdExperience);

            var updateCommand = new
            {
                companyName = "New Company",
                jobTitle = "Senior Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "Updated description"
            };

            var response =
                await _client.PutAsJsonAsync(
                    $"/api/v1/profile/experiences/{createdExperience.Id}",
                    updateCommand);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var updatedExperience =
                await response.Content
                    .ReadFromJsonAsync<ProfessionalExperienceDto>();

            Assert.NotNull(updatedExperience);
            Assert.Equal(createdExperience.Id, updatedExperience.Id);
            Assert.Equal("New Company", updatedExperience.CompanyName);
            Assert.Equal("Senior Data Engineer", updatedExperience.JobTitle);
            Assert.Equal("Paris", updatedExperience.Location);
            Assert.Equal(new DateOnly(2024, 1, 1), updatedExperience.StartDate);
            Assert.Null(updatedExperience.EndDate);
            Assert.True(updatedExperience.IsCurrent);
            Assert.Equal("Updated description", updatedExperience.Description);
            var getResponse =
    await _client.GetAsync(
        "/api/v1/profile/experiences");

            Assert.Equal(
                HttpStatusCode.OK,
                getResponse.StatusCode);

            var experiences =
                await getResponse.Content
                    .ReadFromJsonAsync<List<ProfessionalExperienceDto>>();

            Assert.NotNull(experiences);

            var persistedExperience =
                Assert.Single(experiences);

            Assert.Equal(
                createdExperience.Id,
                persistedExperience.Id);

            Assert.Equal(
                "New Company",
                persistedExperience.CompanyName);

            Assert.Equal(
                "Senior Data Engineer",
                persistedExperience.JobTitle);

            Assert.Equal(
                "Paris",
                persistedExperience.Location);

            Assert.Equal(
                new DateOnly(2024, 1, 1),
                persistedExperience.StartDate);

            Assert.Null(persistedExperience.EndDate);
            Assert.True(persistedExperience.IsCurrent);

            Assert.Equal(
                "Updated description",
                persistedExperience.Description);
        }
        [Fact]
        public async Task UpdateExperience_ShouldReturnNotFound_WhenExperienceDoesNotExist()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var updateCommand = new
            {
                companyName = "New Company",
                jobTitle = "Senior Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "Updated description"
            };

            var response =
                await _client.PutAsJsonAsync(
                    $"/api/v1/profile/experiences/{Guid.NewGuid()}",
                    updateCommand);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);

            var problem =
                await response.Content
                    .ReadFromJsonAsync<ProblemDetails>();

            Assert.NotNull(problem);
            Assert.Equal(404, problem.Status);
            Assert.Equal(
                "Professional experience not found",
                problem.Title);
        }
        [Fact]
        public async Task UpdateExperience_ShouldReturnBadRequest_WhenCurrentExperienceHasEndDate()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var createExperienceCommand = new
            {
                companyName = "Test Company",
                jobTitle = "Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "Test experience"
            };

            var createResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    createExperienceCommand);

            var createdExperience =
                await createResponse.Content
                    .ReadFromJsonAsync<ProfessionalExperienceDto>();

            Assert.NotNull(createdExperience);

            var invalidUpdateCommand = new
            {
                companyName = "Test Company",
                jobTitle = "Senior Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = "2025-01-01",
                isCurrent = true,
                description = "Invalid update"
            };

            var response =
                await _client.PutAsJsonAsync(
                    $"/api/v1/profile/experiences/{createdExperience.Id}",
                    invalidUpdateCommand);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }
        [Fact]
        public async Task DeleteExperience_ShouldReturnNoContent_AndRemoveExperience_WhenExperienceExists()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var experienceCommand = new
            {
                companyName = "Test Company",
                jobTitle = "Data Engineer",
                location = "Paris",
                startDate = "2024-01-01",
                endDate = (string?)null,
                isCurrent = true,
                description = "Experience to delete"
            };

            var createResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile/experiences",
                    experienceCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdExperience =
                await createResponse.Content
                    .ReadFromJsonAsync<ProfessionalExperienceDto>();

            Assert.NotNull(createdExperience);

            var deleteResponse =
                await _client.DeleteAsync(
                    $"/api/v1/profile/experiences/{createdExperience.Id}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            var getResponse =
                await _client.GetAsync(
                    "/api/v1/profile/experiences");

            Assert.Equal(
                HttpStatusCode.OK,
                getResponse.StatusCode);

            var experiences =
                await getResponse.Content
                    .ReadFromJsonAsync<List<ProfessionalExperienceDto>>();

            Assert.NotNull(experiences);
            Assert.Empty(experiences);
        }
        [Fact]
        public async Task DeleteExperience_ShouldReturnNotFound_WhenExperienceDoesNotExist()
        {
            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = "0612345678",
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse =
                await _client.PostAsJsonAsync(
                    "/api/v1/profile",
                    profileCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var experienceId = Guid.NewGuid();

            var response =
                await _client.DeleteAsync(
                    $"/api/v1/profile/experiences/{experienceId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);

            var problem =
                await response.Content
                    .ReadFromJsonAsync<ProblemDetails>();

            Assert.NotNull(problem);
            Assert.Equal(404, problem.Status);
            Assert.Equal(
                "Professional experience not found",
                problem.Title);

            Assert.Contains(
                experienceId.ToString(),
                problem.Detail);
        }
        [Fact]
        public async Task PostEducation_ShouldReturnCreated_WhenEducationIsValid()
        {
            await _factory.ResetDatabaseAsync();

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                new
                {
                    firstName = "Test",
                    lastName = "Candidate",
                    email = "education@example.com",
                    phoneNumber = (string?)null,
                    jobTitle = "Data Engineer",
                    summary = "Integration test profile"
                });

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var command = new
            {
                institutionName = "UniversitÃ© Paris-Saclay",
                degree = "Master",
                fieldOfStudy = "Data Science",
                location = "Paris",
                startDate = "2022-09-01",
                endDate = "2024-06-30",
                isCurrent = false,
                description = "Master spÃ©cialisÃ© en Data Science."
            };

            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile/educations",
                command);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var education =
                await response.Content.ReadFromJsonAsync<EducationDto>();

            Assert.NotNull(education);
            Assert.NotEqual(Guid.Empty, education.Id);
            Assert.Equal(
                "UniversitÃ© Paris-Saclay",
                education.InstitutionName);
            Assert.Equal("Master", education.Degree);
            Assert.Equal("Data Science", education.FieldOfStudy);
            Assert.Equal("Paris", education.Location);
            Assert.Equal(
                new DateOnly(2022, 9, 1),
                education.StartDate);
            Assert.Equal(
                new DateOnly(2024, 6, 30),
                education.EndDate);
            Assert.False(education.IsCurrent);
            var persistedEducation = await _factory.GetEducationAsync(education.Id);

            Assert.NotNull(persistedEducation);

            Assert.Equal(
                education.Id,
                persistedEducation.Id);

            Assert.Equal(
                "UniversitÃ© Paris-Saclay",
                persistedEducation.InstitutionName);

            Assert.Equal(
                "Master",
                persistedEducation.Degree);

            Assert.Equal(
                "Data Science",
                persistedEducation.FieldOfStudy);

            Assert.Equal(
                new DateOnly(2022, 9, 1),
                persistedEducation.StartDate);

            Assert.Equal(
                new DateOnly(2024, 6, 30),
                persistedEducation.EndDate);
        }
        [Fact]
        public async Task GetEducations_ShouldReturnPersistedEducations()
        {
            await _factory.ResetDatabaseAsync();

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                new
                {
                    firstName = "Test",
                    lastName = "Candidate",
                    email = "education-get@example.com",
                    phoneNumber = (string?)null,
                    jobTitle = "Data Engineer",
                    summary = "Integration test profile"
                });

            Assert.Equal(
                HttpStatusCode.Created,
                profileResponse.StatusCode);

            var educationResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/educations",
                new
                {
                    institutionName = "UniversitÃ© Paris-Saclay",
                    degree = "Master",
                    fieldOfStudy = "Data Science",
                    location = "Paris",
                    startDate = "2022-09-01",
                    endDate = "2024-06-30",
                    isCurrent = false,
                    description = "Master spÃ©cialisÃ© en Data Science."
                });

            Assert.Equal(
                HttpStatusCode.Created,
                educationResponse.StatusCode);

            var response = await _client.GetAsync(
                "/api/v1/profile/educations");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var educations = await response.Content.ReadFromJsonAsync<List<EducationDto>>();

            Assert.NotNull(educations);

            var education = Assert.Single(educations);

            Assert.NotEqual(Guid.Empty, education.Id);
            Assert.Equal(
                "UniversitÃ© Paris-Saclay",
                education.InstitutionName);
            Assert.Equal("Master", education.Degree);
            Assert.Equal(
                "Data Science",
                education.FieldOfStudy);
            Assert.Equal("Paris", education.Location);
            Assert.Equal(
                new DateOnly(2022, 9, 1),
                education.StartDate);
            Assert.Equal(
                new DateOnly(2024, 6, 30),
                education.EndDate);
            Assert.False(education.IsCurrent);
            Assert.Equal(
                "Master spÃ©cialisÃ© en Data Science.",
                education.Description);
        }
        [Fact]
        public async Task UpdateEducation_ShouldUpdateAndPersistEducation()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                institutionName = "UniversitÃ© A",
                degree = "Licence",
                fieldOfStudy = "Informatique",
                location = "Paris",
                startDate = "2018-09-01",
                endDate = "2021-06-30",
                isCurrent = false,
                description = "Formation initiale"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/educations",
                createCommand);

            createResponse.EnsureSuccessStatusCode();

            var createdEducation =
                await createResponse.Content.ReadFromJsonAsync<EducationDto>();

            Assert.NotNull(createdEducation);

            var updateCommand = new
            {
                institutionName = "UniversitÃ© Paris-Saclay",
                degree = "Master",
                fieldOfStudy = "Data Science",
                location = "Paris",
                startDate = "2022-09-01",
                endDate = "2024-06-30",
                isCurrent = false,
                description = "Formation mise Ã  jour"
            };

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/profile/educations/{createdEducation.Id}",
                updateCommand);

            Assert.Equal(
                HttpStatusCode.OK,
                updateResponse.StatusCode);

            var updatedEducation =
                await updateResponse.Content.ReadFromJsonAsync<EducationDto>();

            Assert.NotNull(updatedEducation);

            Assert.Equal(
                createdEducation.Id,
                updatedEducation.Id);

            Assert.Equal(
                "UniversitÃ© Paris-Saclay",
                updatedEducation.InstitutionName);

            Assert.Equal(
                "Master",
                updatedEducation.Degree);

            Assert.Equal(
                "Data Science",
                updatedEducation.FieldOfStudy);

            Assert.Equal(
                new DateOnly(2022, 9, 1),
                updatedEducation.StartDate);

            Assert.Equal(
                new DateOnly(2024, 6, 30),
                updatedEducation.EndDate);

            // VÃ©rification de la persistance via GET
            var getResponse = await _client.GetAsync(
                "/api/v1/profile/educations");

            getResponse.EnsureSuccessStatusCode();

            var educations =
                await getResponse.Content
                    .ReadFromJsonAsync<List<EducationDto>>();

            Assert.NotNull(educations);

            var persistedEducation =
                Assert.Single(educations);

            Assert.Equal(
                createdEducation.Id,
                persistedEducation.Id);

            Assert.Equal(
                "UniversitÃ© Paris-Saclay",
                persistedEducation.InstitutionName);

            Assert.Equal(
                "Master",
                persistedEducation.Degree);

            Assert.Equal(
                "Data Science",
                persistedEducation.FieldOfStudy);

            Assert.Equal(
                "Formation mise Ã  jour",
                persistedEducation.Description);
        }

        [Fact]
        public async Task UpdateEducation_ShouldReturnNotFound_WhenEducationDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var command = new
            {
                institutionName = "UniversitÃ© Paris-Saclay",
                degree = "Master",
                fieldOfStudy = "Data Science",
                location = "Paris",
                startDate = "2022-09-01",
                endDate = "2024-06-30",
                isCurrent = false,
                description = "Formation"
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1/profile/educations/{Guid.NewGuid()}",
                command);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }

        [Fact]
        public async Task UpdateEducation_ShouldReturnBadRequest_WhenCurrentEducationHasEndDate()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                institutionName = "UniversitÃ© A",
                degree = "Licence",
                fieldOfStudy = "Informatique",
                location = "Paris",
                startDate = "2018-09-01",
                endDate = "2021-06-30",
                isCurrent = false,
                description = "Formation initiale"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/educations",
                createCommand);

            createResponse.EnsureSuccessStatusCode();

            var education =
                await createResponse.Content.ReadFromJsonAsync<EducationDto>();

            Assert.NotNull(education);

            var invalidCommand = new
            {
                institutionName = "UniversitÃ© Paris-Saclay",
                degree = "Master",
                fieldOfStudy = "Data Science",
                location = "Paris",
                startDate = "2024-09-01",
                endDate = "2026-06-30",
                isCurrent = true,
                description = "Formation en cours"
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1/profile/educations/{education.Id}",
                invalidCommand);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                response.StatusCode);
        }
        [Fact]
        public async Task DeleteEducation_ShouldDeleteAndPersistEducation()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                institutionName = "UniversitÃ© Paris-Saclay",
                degree = "Master",
                fieldOfStudy = "Data Science",
                location = "Paris",
                startDate = "2022-09-01",
                endDate = "2024-06-30",
                isCurrent = false,
                description = "Formation Ã  supprimer"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/educations",
                createCommand);

            createResponse.EnsureSuccessStatusCode();

            var education =
                await createResponse.Content.ReadFromJsonAsync<EducationDto>();

            Assert.NotNull(education);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/v1/profile/educations/{education.Id}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            // VÃ©rification de la persistance de la suppression
            var getResponse = await _client.GetAsync(
                "/api/v1/profile/educations");

            getResponse.EnsureSuccessStatusCode();

            var educations =
                await getResponse.Content
                    .ReadFromJsonAsync<List<EducationDto>>();

            Assert.NotNull(educations);
            Assert.Empty(educations);
        }

        [Fact]
        public async Task DeleteEducation_ShouldReturnNotFound_WhenEducationDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var educationId = Guid.NewGuid();

            var response = await _client.DeleteAsync(
                $"/api/v1/profile/educations/{educationId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }
        [Fact]
        public async Task PostSkill_ShouldReturnCreated_WhenSkillIsValid()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var command = new
            {
                name = "Databricks",
                category = "Data Engineering",
                level = "Advanced",
                yearsOfExperience = 4
            };

            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile/skills",
                command);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var skill =
                await response.Content.ReadFromJsonAsync<SkillDto>();

            Assert.NotNull(skill);

            Assert.NotEqual(Guid.Empty, skill.Id);
            Assert.Equal("Databricks", skill.Name);
            Assert.Equal("Data Engineering", skill.Category);
            Assert.Equal("Advanced", skill.Level);
            Assert.Equal(4, skill.YearsOfExperience);

            var persistedSkill =
                await _factory.GetSkillAsync(skill.Id);

            Assert.NotNull(persistedSkill);

            Assert.Equal(skill.Id, persistedSkill.Id);
            Assert.Equal("Databricks", persistedSkill.Name);
            Assert.Equal(
                "Data Engineering",
                persistedSkill.Category);
            Assert.Equal("Advanced", persistedSkill.Level);
            Assert.Equal(4, persistedSkill.YearsOfExperience);
        }
        [Fact]
        public async Task GetSkills_ShouldReturnPersistedSkillsOrderedByName()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var skills = new[]
            {
                new
                {
                    name = "Python",
                    category = "Programming",
                    level = "Advanced",
                    yearsOfExperience = 5
                },
                new
                {
                    name = "Azure",
                    category = "Cloud",
                    level = "Intermediate",
                    yearsOfExperience = 3
                },
                new
                {
                    name = "Databricks",
                    category = "Data Engineering",
                    level = "Advanced",
                    yearsOfExperience = 4
                }
            };

            foreach (var skill in skills)
            {
                var createResponse = await _client.PostAsJsonAsync(
                    "/api/v1/profile/skills",
                    skill);

                Assert.Equal(
                    HttpStatusCode.Created,
                    createResponse.StatusCode);
            }

            var response = await _client.GetAsync(
                "/api/v1/profile/skills");

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var result =
                await response.Content
                    .ReadFromJsonAsync<List<SkillDto>>();

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);

            Assert.Equal(
                new[] { "Azure", "Databricks", "Python" },
                result.Select(x => x.Name));

            Assert.Equal("Cloud", result[0].Category);
            Assert.Equal("Data Engineering", result[1].Category);
            Assert.Equal("Programming", result[2].Category);
        }
        [Fact]
        public async Task UpdateSkill_ShouldUpdateAndPersistSkill()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                name = "Databricks",
                category = "Data Engineering",
                level = "Intermediate",
                yearsOfExperience = 2
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/skills",
                createCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdSkill =
                await createResponse.Content.ReadFromJsonAsync<SkillDto>();

            Assert.NotNull(createdSkill);

            var updateCommand = new
            {
                name = "Databricks",
                category = "Data Platform",
                level = "Advanced",
                yearsOfExperience = 4
            };

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/profile/skills/{createdSkill.Id}",
                updateCommand);

            Assert.Equal(
                HttpStatusCode.OK,
                updateResponse.StatusCode);

            var updatedSkill =
                await updateResponse.Content.ReadFromJsonAsync<SkillDto>();

            Assert.NotNull(updatedSkill);

            Assert.Equal(createdSkill.Id, updatedSkill.Id);
            Assert.Equal("Databricks", updatedSkill.Name);
            Assert.Equal("Data Platform", updatedSkill.Category);
            Assert.Equal("Advanced", updatedSkill.Level);
            Assert.Equal(4, updatedSkill.YearsOfExperience);

            var persistedSkill =
                await _factory.GetSkillAsync(createdSkill.Id);

            Assert.NotNull(persistedSkill);

            Assert.Equal(createdSkill.Id, persistedSkill.Id);
            Assert.Equal("Databricks", persistedSkill.Name);
            Assert.Equal("Data Platform", persistedSkill.Category);
            Assert.Equal("Advanced", persistedSkill.Level);
            Assert.Equal(4, persistedSkill.YearsOfExperience);
        }
        [Fact]
        public async Task UpdateSkill_ShouldReturnNotFound_WhenSkillDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var unknownSkillId = Guid.NewGuid();

            var command = new
            {
                name = "Databricks",
                category = "Data Engineering",
                level = "Advanced",
                yearsOfExperience = 4
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1/profile/skills/{unknownSkillId}",
                command);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }
        [Fact]
        public async Task DeleteSkill_ShouldDeleteAndPersistSkill()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var skillCommand = new
            {
                name = "Databricks",
                category = "Data Engineering",
                level = "Advanced",
                yearsOfExperience = 4
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/skills",
                skillCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdSkill =
                await createResponse.Content.ReadFromJsonAsync<SkillDto>();

            Assert.NotNull(createdSkill);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/v1/profile/skills/{createdSkill.Id}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            var persistedSkill =
                await _factory.GetSkillAsync(createdSkill.Id);

            Assert.Null(persistedSkill);

            var getResponse = await _client.GetAsync("/api/v1/profile/skills");

            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var skills = await getResponse.Content.ReadFromJsonAsync<List<SkillDto>>();

            Assert.NotNull(skills);
            Assert.Empty(skills);
        }
        [Fact]
        public async Task DeleteSkill_ShouldReturnNotFound_WhenSkillDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var unknownSkillId = Guid.NewGuid();

            var response = await _client.DeleteAsync(
                $"/api/v1/profile/skills/{unknownSkillId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }
        [Fact]
        public async Task PostLanguage_ShouldCreateLanguage()
        {
            await _factory.ResetDatabaseAsync();

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                new
                {
                    firstName = "Test",
                    lastName = "Candidate",
                    email = "test@example.com"
                });

            Assert.Equal(HttpStatusCode.Created, profileResponse.StatusCode);

            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile/languages",
                new
                {
                    name = "French",
                    proficiencyLevel = "Native"
                });

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var language =
                await response.Content
                    .ReadFromJsonAsync<LanguageDto>();

            Assert.NotNull(language);
            Assert.NotEqual(Guid.Empty, language.Id);
            Assert.Equal("French", language.Name);
            Assert.Equal(
                "Native",
                language.ProficiencyLevel);

            var persistedLanguage =
                await _factory.GetLanguageAsync(language.Id);

            Assert.NotNull(persistedLanguage);
            Assert.Equal(
                "French",
                persistedLanguage.Name);
            Assert.Equal(
                "Native",
                persistedLanguage.ProficiencyLevel);
        }
        [Fact]
        public async Task PostLanguage_ShouldReturnCreated_WhenLanguageIsValid()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var command = new
            {
                name = "French",
                proficiencyLevel = "Native"
            };

            var response = await _client.PostAsJsonAsync(
                "/api/v1/profile/languages",
                command);

            Assert.Equal(
                HttpStatusCode.Created,
                response.StatusCode);

            var language =
                await response.Content.ReadFromJsonAsync<LanguageDto>();

            Assert.NotNull(language);

            Assert.NotEqual(Guid.Empty, language.Id);
            Assert.Equal("French", language.Name);
            Assert.Equal(
                "Native",
                language.ProficiencyLevel);

            var persistedLanguage =
                await _factory.GetLanguageAsync(language.Id);

            Assert.NotNull(persistedLanguage);

            Assert.Equal(language.Id, persistedLanguage.Id);
            Assert.Equal("French", persistedLanguage.Name);
            Assert.Equal(
                "Native",
                persistedLanguage.ProficiencyLevel);
        }
        [Fact]
        public async Task GetLanguages_ShouldReturnPersistedLanguagesOrderedByName()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var languages = new[]
            {
        new
        {
            name = "Spanish",
            proficiencyLevel = "Intermediate"
        },
        new
        {
            name = "English",
            proficiencyLevel = "Fluent"
        },
        new
        {
            name = "French",
            proficiencyLevel = "Native"
        }
    };

            foreach (var language in languages)
            {
                var response = await _client.PostAsJsonAsync(
                    "/api/v1/profile/languages",
                    language);

                Assert.Equal(
                    HttpStatusCode.Created,
                    response.StatusCode);
            }

            var getResponse = await _client.GetAsync(
                "/api/v1/profile/languages");

            Assert.Equal(
                HttpStatusCode.OK,
                getResponse.StatusCode);

            var result =
                await getResponse.Content
                    .ReadFromJsonAsync<List<LanguageDto>>();

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);

            Assert.Equal("English", result[0].Name);
            Assert.Equal("Fluent", result[0].ProficiencyLevel);

            Assert.Equal("French", result[1].Name);
            Assert.Equal("Native", result[1].ProficiencyLevel);

            Assert.Equal("Spanish", result[2].Name);
            Assert.Equal(
                "Intermediate",
                result[2].ProficiencyLevel);
        }
        [Fact]
        public async Task PutLanguage_ShouldUpdatePersistedLanguage()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                name = "French",
                proficiencyLevel = "Intermediate"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/languages",
                createCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdLanguage =
                await createResponse.Content
                    .ReadFromJsonAsync<LanguageDto>();

            Assert.NotNull(createdLanguage);

            var updateCommand = new
            {
                name = "French",
                proficiencyLevel = "Native"
            };

            var updateResponse = await _client.PutAsJsonAsync(
                $"/api/v1/profile/languages/{createdLanguage.Id}",
                updateCommand);

            Assert.Equal(
                HttpStatusCode.OK,
                updateResponse.StatusCode);

            var updatedLanguage =
                await updateResponse.Content
                    .ReadFromJsonAsync<LanguageDto>();

            Assert.NotNull(updatedLanguage);

            Assert.Equal(
                createdLanguage.Id,
                updatedLanguage.Id);

            Assert.Equal(
                "French",
                updatedLanguage.Name);

            Assert.Equal(
                "Native",
                updatedLanguage.ProficiencyLevel);

            var persistedLanguage =
                await _factory.GetLanguageAsync(
                    createdLanguage.Id);

            Assert.NotNull(persistedLanguage);

            Assert.Equal(
                createdLanguage.Id,
                persistedLanguage.Id);

            Assert.Equal(
                "French",
                persistedLanguage.Name);

            Assert.Equal(
                "Native",
                persistedLanguage.ProficiencyLevel);
        }
        [Fact]
        public async Task PutLanguage_ShouldReturnNotFound_WhenLanguageDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var unknownLanguageId = Guid.NewGuid();

            var command = new
            {
                name = "French",
                proficiencyLevel = "Native"
            };

            var response = await _client.PutAsJsonAsync(
                $"/api/v1/profile/languages/{unknownLanguageId}",
                command);

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }
        [Fact]
        public async Task DeleteLanguage_ShouldDeletePersistedLanguage()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var createCommand = new
            {
                name = "French",
                proficiencyLevel = "Native"
            };

            var createResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile/languages",
                createCommand);

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var createdLanguage =
                await createResponse.Content
                    .ReadFromJsonAsync<LanguageDto>();

            Assert.NotNull(createdLanguage);

            var deleteResponse = await _client.DeleteAsync(
                $"/api/v1/profile/languages/{createdLanguage.Id}");

            Assert.Equal(
                HttpStatusCode.NoContent,
                deleteResponse.StatusCode);

            var persistedLanguage =
                await _factory.GetLanguageAsync(
                    createdLanguage.Id);

            Assert.Null(persistedLanguage);
        }
        [Fact]
        public async Task DeleteLanguage_ShouldReturnNotFound_WhenLanguageDoesNotExist()
        {
            await _factory.ResetDatabaseAsync();

            var profileCommand = new
            {
                firstName = "Test",
                lastName = "Candidate",
                email = "test@example.com",
                phoneNumber = (string?)null,
                jobTitle = "Data Engineer",
                summary = "Integration test profile"
            };

            var profileResponse = await _client.PostAsJsonAsync(
                "/api/v1/profile",
                profileCommand);

            profileResponse.EnsureSuccessStatusCode();

            var unknownLanguageId = Guid.NewGuid();

            var response = await _client.DeleteAsync(
                $"/api/v1/profile/languages/{unknownLanguageId}");

            Assert.Equal(
                HttpStatusCode.NotFound,
                response.StatusCode);
        }

        private async Task CreateProfileForLinksAsync()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/profile",
                new { firstName = "Test", lastName = "Candidate", email = "links@example.com" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Links_ShouldPersistCreateUpdateAndDelete()
        {
            await CreateProfileForLinksAsync();
            const string route = "/api/v1/profile/links";
            Assert.Empty((await _client.GetFromJsonAsync<List<LinkDto>>(route))!);
            var response = await _client.PostAsJsonAsync(route,
                new { name = "Portfolio", url = "https://example.com/portfolio" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<LinkDto>();
            Assert.NotNull(created);
            Assert.NotEqual(Guid.Empty, created.Id);
            var persisted = Assert.Single((await _client.GetFromJsonAsync<List<LinkDto>>(route))!);
            Assert.Equal(created.Id, persisted.Id);
            Assert.Equal("Portfolio", persisted.Name);
            Assert.Equal("https://example.com/portfolio", persisted.Url);

            var updated = await _client.PutAsJsonAsync($"{route}/{created.Id}",
                new { name = "GitHub", url = "https://github.com/example" });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            persisted = Assert.Single((await _client.GetFromJsonAsync<List<LinkDto>>(route))!);
            Assert.Equal(created.Id, persisted.Id);
            Assert.Equal("GitHub", persisted.Name);
            Assert.Equal("https://github.com/example", persisted.Url);

            Assert.Equal(HttpStatusCode.NoContent,
                (await _client.DeleteAsync($"{route}/{created.Id}")).StatusCode);
            Assert.Empty((await _client.GetFromJsonAsync<List<LinkDto>>(route))!);
        }

        [Fact]
        public async Task Links_ShouldReturnNotFound_WhenProfileIsMissing()
        {
            const string route = "/api/v1/profile/links";
            var command = new { name = "Portfolio", url = "https://example.com" };
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(route, command)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"{route}/{Guid.NewGuid()}", command)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{route}/{Guid.NewGuid()}")).StatusCode);
        }

        [Fact]
        public async Task Links_ShouldReturnNotFound_WhenLinkIsMissing()
        {
            await CreateProfileForLinksAsync();
            var route = $"/api/v1/profile/links/{Guid.NewGuid()}";
            Assert.Equal(HttpStatusCode.NotFound,
                (await _client.PutAsJsonAsync(route, new { name = "Portfolio", url = "https://example.com" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(route)).StatusCode);
        }

        [Theory]
        [InlineData("", "https://example.com")]
        [InlineData("Portfolio", "")]
        [InlineData("Portfolio", "/relative")]
        [InlineData("Portfolio", "javascript:alert(1)")]
        [InlineData("Portfolio", "ftp://example.com")]
        public async Task Links_ShouldRejectInvalidCommands_WithoutChangingPersistedLink(string name, string url)
        {
            await CreateProfileForLinksAsync();
            const string route = "/api/v1/profile/links";
            var response = await _client.PostAsJsonAsync(route,
                new { name = "Portfolio", url = "https://example.com" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<LinkDto>();
            var before = Assert.Single((await _client.GetFromJsonAsync<List<LinkDto>>(route))!);
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(route, new { name, url })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"{route}/{created!.Id}", new { name, url })).StatusCode);
            Assert.Equal(before, Assert.Single((await _client.GetFromJsonAsync<List<LinkDto>>(route))!));
        }

        private static UpdatePreferencesCommand ValidPreferencesCommand() =>
            new(["Backend Developer", "Data Engineer"], ["Paris", "Lyon"], ["CDI", "Freelance"],
                ["Hybrid", "Remote"], 55000.25m, "EUR", new DateOnly(2027, 1, 1));

        private async Task CreateProfileForPreferencesAsync()
        {
            var response = await _client.PostAsJsonAsync("/api/v1/profile",
                new { firstName = "Test", lastName = "Candidate", email = "preferences@example.com" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Preferences_ShouldPersistCreateUpdateDeleteAndRecreate()
        {
            await CreateProfileForPreferencesAsync();
            const string route = "/api/v1/profile/preferences";
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(route)).StatusCode);
            var command = ValidPreferencesCommand();
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(route, command)).StatusCode);
            var response = await _client.PostAsJsonAsync(route, command);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.EndsWith(route, response.Headers.Location!.ToString());
            var beforeDuplicate = await _client.GetStringAsync(route);
            Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync(route,
                new UpdatePreferencesCommand([], [], [], [], null, null, null))).StatusCode);
            Assert.Equal(beforeDuplicate, await _client.GetStringAsync(route));
            var initial = await _client.GetFromJsonAsync<PreferencesDto>(route);
            Assert.NotNull(initial);
            Assert.Equal(command.DesiredJobTitles, initial.DesiredJobTitles);
            Assert.Equal(command.PreferredLocations, initial.PreferredLocations);
            Assert.Equal(command.ContractTypes, initial.ContractTypes);
            Assert.Equal(command.WorkModes, initial.WorkModes);
            Assert.Equal(command.MinimumAnnualGrossSalary, initial.MinimumAnnualGrossSalary);
            Assert.Equal("EUR", initial.SalaryCurrency);
            Assert.Equal(command.AvailableFrom, initial.AvailableFrom);

            var replacement = new UpdatePreferencesCommand(["Architect"], [], [], ["OnSite"], null, null, null);
            response = await _client.PutAsJsonAsync(route, replacement);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await _client.GetFromJsonAsync<PreferencesDto>(route);
            Assert.NotNull(updated);
            Assert.Equal(initial.CandidateProfileId, updated.CandidateProfileId);
            Assert.Equal(initial.CreatedAtUtc, updated.CreatedAtUtc);
            Assert.Equal(replacement.DesiredJobTitles, updated.DesiredJobTitles);
            Assert.Empty(updated.PreferredLocations);
            Assert.Empty(updated.ContractTypes);
            Assert.Equal(replacement.WorkModes, updated.WorkModes);
            Assert.Null(updated.MinimumAnnualGrossSalary);
            Assert.Null(updated.SalaryCurrency);
            Assert.Null(updated.AvailableFrom);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
                Assert.Equal(1, await db.Preferences.CountAsync());
            }

            Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(route)).StatusCode);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ProfileDbContext>();
                Assert.Equal(0, await db.Preferences.CountAsync());
                Assert.Equal(1, await db.CandidateProfiles.CountAsync());
            }
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(route, command)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(route, command)).StatusCode);
            Assert.Equal(command.DesiredJobTitles,
                (await _client.GetFromJsonAsync<PreferencesDto>(route))!.DesiredJobTitles);

            // Deleting the parent must also remove its preferences.
            await _factory.ResetDatabaseAsync();
            using var finalScope = _factory.Services.CreateScope();
            Assert.Equal(0, await finalScope.ServiceProvider.GetRequiredService<ProfileDbContext>().Preferences.CountAsync());
        }

        [Fact]
        public async Task Preferences_ShouldReturnNotFound_WhenProfileIsMissing()
        {
            const string route = "/api/v1/profile/preferences";
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(route, ValidPreferencesCommand())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(route, ValidPreferencesCommand())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync(route)).StatusCode);
        }

        [Fact]
        public async Task Preferences_ShouldAcceptEmptyCriteria()
        {
            await CreateProfileForPreferencesAsync();
            const string route = "/api/v1/profile/preferences";
            var response = await _client.PostAsJsonAsync(route,
                new UpdatePreferencesCommand([], [], [], [], null, null, null));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var dto = await _client.GetFromJsonAsync<PreferencesDto>(route);
            Assert.NotNull(dto);
            Assert.Empty(dto.DesiredJobTitles);
            Assert.Empty(dto.PreferredLocations);
            Assert.Empty(dto.ContractTypes);
            Assert.Empty(dto.WorkModes);
            Assert.Null(dto.MinimumAnnualGrossSalary);
            Assert.Null(dto.SalaryCurrency);
            Assert.Null(dto.AvailableFrom);
        }

        [Theory]
        [InlineData("titles")]
        [InlineData("nullList")]
        [InlineData("mode")]
        [InlineData("salary")]
        [InlineData("precision")]
        [InlineData("currency")]
        public async Task Preferences_ShouldRejectInvalidCriteriaWithoutChangingStoredPreferences(string scenario)
        {
            await CreateProfileForPreferencesAsync();
            const string route = "/api/v1/profile/preferences";
            var valid = ValidPreferencesCommand();
            Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(route, valid)).StatusCode);
            var before = await _client.GetStringAsync(route);
            var invalid = scenario switch
            {
                "titles" => valid with { DesiredJobTitles = [""] },
                "nullList" => valid with { PreferredLocations = null! },
                "mode" => valid with { WorkModes = ["invalid"] },
                "salary" => valid with { MinimumAnnualGrossSalary = -1 },
                "precision" => valid with { MinimumAnnualGrossSalary = 12.345m },
                _ => valid with { SalaryCurrency = null }
            };
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(route, invalid)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync(route, invalid)).StatusCode);
            Assert.Equal(before, await _client.GetStringAsync(route));
        }

        [Fact]
        public async Task Update_ShouldPersistProfileAndPreserveAssociatedData()
        {
            await CreateProfileForPreferencesAsync();
            const string route = "/api/v1/profile";
            var initial = await _client.GetFromJsonAsync<CandidateProfileDto>(route);
            Assert.NotNull(initial);
            Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync(route + "/links",
                new { name = "Portfolio", url = "https://example.com" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created,
                (await _client.PostAsJsonAsync(route + "/preferences", ValidPreferencesCommand())).StatusCode);
            var linksBefore = await _client.GetStringAsync(route + "/links");
            var preferencesBefore = await _client.GetStringAsync(route + "/preferences");
            var command = new UpdateCandidateProfileCommand("Updated", "Person", "updated@example.com",
                "0612345678", "Architect", "Updated summary");
            var response = await _client.PutAsJsonAsync(route, command);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await _client.GetFromJsonAsync<CandidateProfileDto>(route);
            Assert.NotNull(updated);
            Assert.Equal(initial.Id, updated.Id);
            Assert.Equal(initial.CreatedAtUtc, updated.CreatedAtUtc);
            Assert.Equal(command.FirstName, updated.FirstName);
            Assert.Equal(command.LastName, updated.LastName);
            Assert.Equal(command.Email, updated.Email);
            Assert.Equal(command.PhoneNumber, updated.PhoneNumber);
            Assert.Equal(command.JobTitle, updated.JobTitle);
            Assert.Equal(command.Summary, updated.Summary);
            Assert.Equal(linksBefore, await _client.GetStringAsync(route + "/links"));
            Assert.Equal(preferencesBefore, await _client.GetStringAsync(route + "/preferences"));

            response = await _client.PutAsJsonAsync(route, command with { PhoneNumber = null, JobTitle = null, Summary = null });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            updated = await _client.GetFromJsonAsync<CandidateProfileDto>(route);
            Assert.Null(updated!.PhoneNumber);
            Assert.Null(updated.JobTitle);
            Assert.Null(updated.Summary);
        }

        [Fact]
        public async Task Update_ShouldReturnNotFound_WhenProfileDoesNotExist()
        {
            var response = await _client.PutAsJsonAsync("/api/v1/profile",
                new UpdateCandidateProfileCommand("Test", "Candidate", "test@example.com", null, null, null));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/profile")).StatusCode);
        }

        [Theory]
        [InlineData("firstName")]
        [InlineData("lastName")]
        [InlineData("email")]
        [InlineData("phone")]
        [InlineData("jobTitle")]
        [InlineData("summary")]
        public async Task Update_ShouldRejectInvalidDataWithoutChangingProfile(string field)
        {
            await CreateProfileForPreferencesAsync();
            const string route = "/api/v1/profile";
            var before = await _client.GetStringAsync(route);
            var command = new UpdateCandidateProfileCommand("Test", "Candidate", "test@example.com", null, null, null);
            command = field switch
            {
                "firstName" => command with { FirstName = "" },
                "lastName" => command with { LastName = "" },
                "email" => command with { Email = "invalid" },
                "phone" => command with { PhoneNumber = new string('1', 31) },
                "jobTitle" => command with { JobTitle = new string('a', 151) },
                _ => command with { Summary = new string('a', 2001) }
            };
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync(route, command)).StatusCode);
            Assert.Equal(before, await _client.GetStringAsync(route));
        }
    }
}
