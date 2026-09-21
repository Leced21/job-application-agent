using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Create;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Create
{
    public sealed class CreateCandidateProfileHandlerTests
    {
        private readonly ICandidateProfileRepository _repository;
        private readonly IValidator<CreateCandidateProfileCommand> _validator;
        private readonly CreateCandidateProfileHandler _handler;

        public CreateCandidateProfileHandlerTests()
        {
            _repository = Substitute.For<ICandidateProfileRepository>();

            _validator = new CreateCandidateProfileCommandValidator();

            _handler = new CreateCandidateProfileHandler(
                _repository,
                _validator);
        }

        [Fact]
        public async Task HandleAsync_ShouldCreateProfile_WhenProfileDoesNotExist()
        {
            // Arrange
            var command = CreateValidCommand();

            _repository
                .GetAsync(Arg.Any<CancellationToken>())
                .Returns((CandidateProfile?)null);

            // Act
            var result = await _handler.HandleAsync(command);

            // Assert
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(command.FirstName, result.FirstName);
            Assert.Equal(command.LastName, result.LastName);
            Assert.Equal(command.Email, result.Email);
            Assert.Equal(command.PhoneNumber, result.PhoneNumber);
            Assert.Equal(command.JobTitle, result.JobTitle);
            Assert.Equal(command.Summary, result.Summary);

            await _repository
                .Received(1)
                .AddAsync(
                    Arg.Any<CandidateProfile>(),
                    Arg.Any<CancellationToken>());

            await _repository
                .Received(1)
                .SaveChangesAsync(
                    Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ShouldThrow_WhenProfileAlreadyExists()
        {
            // Arrange
            var command = CreateValidCommand();

            var existingProfile = new CandidateProfile(
                "Existing",
                "Candidate",
                "existing@example.com",
                null,
                "Data Engineer",
                null);

            _repository
                .GetAsync(Arg.Any<CancellationToken>())
                .Returns(existingProfile);

            // Act
            var action = async () =>
                await _handler.HandleAsync(command);

            // Assert
            await Assert.ThrowsAsync<
                CandidateProfileAlreadyExistsException>(action);

            await _repository
                .DidNotReceive()
                .AddAsync(
                    Arg.Any<CandidateProfile>(),
                    Arg.Any<CancellationToken>());

            await _repository
                .DidNotReceive()
                .SaveChangesAsync(
                    Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task HandleAsync_ShouldNotAccessRepository_WhenCommandIsInvalid()
        {
            // Arrange
            var command = new CreateCandidateProfileCommand(
                "",
                "",
                "invalid-email",
                null,
                null,
                null);

            // Act
            var action = async () =>
                await _handler.HandleAsync(command);

            // Assert
            await Assert.ThrowsAsync<ValidationException>(action);

            await _repository
                .DidNotReceive()
                .GetAsync(Arg.Any<CancellationToken>());

            await _repository
                .DidNotReceive()
                .AddAsync(
                    Arg.Any<CandidateProfile>(),
                    Arg.Any<CancellationToken>());

            await _repository
                .DidNotReceive()
                .SaveChangesAsync(
                    Arg.Any<CancellationToken>());
        }

        private static CreateCandidateProfileCommand CreateValidCommand()
        {
            return new CreateCandidateProfileCommand(
                "Test",
                "Candidate",
                "test@example.com",
                null,
                "Data Engineer",
                "Candidate profile");
        }
    }
}