using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Experiences.Get;

public sealed class GetProfessionalExperiencesHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetProfessionalExperiencesHandler _handler;

    public GetProfessionalExperiencesHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _handler = new GetProfessionalExperiencesHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnProfessionalExperiences_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddProfessionalExperience(
            companyName: "First Company",
            jobTitle: "Data Engineer",
            startDate: new DateOnly(2022, 1, 1),
            endDate: new DateOnly(2023, 12, 31),
            isCurrent: false,
            location: "Lyon",
            description: "Première expérience.");

        profile.AddProfessionalExperience(
            companyName: "Current Company",
            jobTitle: "Senior Data Engineer",
            startDate: new DateOnly(2024, 1, 1),
            endDate: null,
            isCurrent: true,
            location: "Paris",
            description: "Expérience actuelle.");

        _repository
            .GetWithProfessionalExperiencesAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.Equal(2, result.Count);

        var experiences = result.ToList();

        Assert.Equal("Current Company", experiences[0].CompanyName);
        Assert.Equal("Senior Data Engineer", experiences[0].JobTitle);
        Assert.Equal(new DateOnly(2024, 1, 1), experiences[0].StartDate);
        Assert.True(experiences[0].IsCurrent);

        Assert.Equal("First Company", experiences[1].CompanyName);
        Assert.Equal(new DateOnly(2022, 1, 1), experiences[1].StartDate);
        Assert.False(experiences[1].IsCurrent);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoExperiences()
    {
        var profile = CreateProfile();

        _repository
            .GetWithProfessionalExperiencesAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowCandidateProfileNotFoundException_WhenProfileDoesNotExist()
    {
        _repository
            .GetWithProfessionalExperiencesAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync());
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