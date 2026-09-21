using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Profile.Application.Profiles;
using JobApplicationAgent.Profile.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    }
}