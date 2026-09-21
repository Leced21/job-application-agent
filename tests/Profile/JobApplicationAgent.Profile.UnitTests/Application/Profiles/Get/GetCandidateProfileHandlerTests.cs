using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Profiles.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Get
{
    public sealed class GetCandidateProfileHandlerTests
    {
        private readonly ICandidateProfileRepository _repository;
        private readonly GetCandidateProfileHandler _handler;

        public GetCandidateProfileHandlerTests()
        {
            _repository = Substitute.For<ICandidateProfileRepository>();

            _handler = new GetCandidateProfileHandler(_repository);
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnProfile_WhenProfileExists()
        {
            // Arrange
            var profile = new CandidateProfile(
                "Test",
                "Candidate",
                "test@example.com",
                "0612345678",
                "Data Engineer",
                "Candidate profile");

            _repository
                .GetAsync(Arg.Any<CancellationToken>())
                .Returns(profile);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.NotNull(result);

            Assert.Equal(profile.Id, result.Id);
            Assert.Equal(profile.FirstName, result.FirstName);
            Assert.Equal(profile.LastName, result.LastName);
            Assert.Equal(profile.Email, result.Email);
            Assert.Equal(profile.PhoneNumber, result.PhoneNumber);
            Assert.Equal(profile.JobTitle, result.JobTitle);
            Assert.Equal(profile.Summary, result.Summary);
            Assert.Equal(profile.CreatedAtUtc, result.CreatedAtUtc);
            Assert.Equal(profile.UpdatedAtUtc, result.UpdatedAtUtc);

            await _repository
                .Received(1)
                .GetAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ShouldReturnNull_WhenProfileDoesNotExist()
        {
            // Arrange
            _repository
                .GetAsync(Arg.Any<CancellationToken>())
                .Returns((CandidateProfile?)null);

            // Act
            var result = await _handler.HandleAsync();

            // Assert
            Assert.Null(result);

            await _repository
                .Received(1)
                .GetAsync(Arg.Any<CancellationToken>());
        }
    }
}