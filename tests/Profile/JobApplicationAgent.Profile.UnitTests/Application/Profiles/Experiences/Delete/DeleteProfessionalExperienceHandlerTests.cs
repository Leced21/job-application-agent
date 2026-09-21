using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Experiences.Delete;

public sealed class DeleteProfessionalExperienceHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly DeleteProfessionalExperienceHandler _handler;

    public DeleteProfessionalExperienceHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _handler = new DeleteProfessionalExperienceHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteProfessionalExperience_WhenExperienceExists()
    {
        var profile = CreateProfile();

        var experience = profile.AddProfessionalExperience(
            companyName: "Test Company",
            jobTitle: "Data Engineer",
            startDate: new DateOnly(2024, 1, 1),
            endDate: null,
            isCurrent: true,
            location: "Paris",
            description: "Test experience");

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        await _handler.HandleAsync(experience.Id);

        Assert.Empty(profile.ProfessionalExperiences);

        await _repository.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowCandidateProfileNotFoundException_WhenProfileDoesNotExist()
    {
        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowProfessionalExperienceNotFoundException_WhenExperienceDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        await Assert.ThrowsAsync<ProfessionalExperienceNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CandidateProfile CreateProfile()
    {
        return new CandidateProfile(
            firstName: "Test",
            lastName: "Candidate",
            email: "test@example.com",
            phoneNumber: "0612345678",
            jobTitle: "Data Engineer",
            summary: "Test profile");
    }
}