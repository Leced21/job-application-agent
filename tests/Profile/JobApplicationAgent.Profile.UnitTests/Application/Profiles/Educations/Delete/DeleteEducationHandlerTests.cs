using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Educations.Delete;

public sealed class DeleteEducationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly DeleteEducationHandler _handler;

    public DeleteEducationHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new DeleteEducationHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteEducation_WhenEducationExists()
    {
        var profile = CreateProfile();

        var education = profile.AddEducation(
            "Université Paris-Saclay",
            "Master",
            new DateOnly(2022, 9, 1),
            new DateOnly(2024, 6, 30),
            false,
            "Data Science",
            "Paris",
            "Formation");

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        await _handler.HandleAsync(education.Id);

        Assert.Empty(profile.Educations);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenEducationDoesNotExist()
    {
        var profile = CreateProfile();
        var educationId = Guid.NewGuid();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var exception =
            await Assert.ThrowsAsync<EducationNotFoundException>(
                () => _handler.HandleAsync(educationId));

        Assert.Contains(
            educationId.ToString(),
            exception.Message);

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    private static CandidateProfile CreateProfile()
    {
        return new CandidateProfile(
            "Test",
            "Candidate",
            "test@example.com",
            null,
            "Data Engineer",
            "Test profile");
    }
}