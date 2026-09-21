using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Educations.Get;

public sealed class GetEducationsHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetEducationsHandler _handler;

    public GetEducationsHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _handler = new GetEducationsHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEducations_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddEducation(
            institutionName: "Université Paris-Saclay",
            degree: "Master",
            startDate: new DateOnly(2022, 9, 1),
            endDate: new DateOnly(2024, 6, 30),
            isCurrent: false,
            fieldOfStudy: "Data Science",
            location: "Paris",
            description: "Master en Data Science.");

        _repository
            .GetWithEducationsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        var education = Assert.Single(result);

        Assert.Equal("Université Paris-Saclay", education.InstitutionName);
        Assert.Equal("Master", education.Degree);
        Assert.Equal("Data Science", education.FieldOfStudy);
        Assert.Equal("Paris", education.Location);
        Assert.Equal(new DateOnly(2022, 9, 1), education.StartDate);
        Assert.Equal(new DateOnly(2024, 6, 30), education.EndDate);
        Assert.False(education.IsCurrent);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEducationsOrderedByStartDateDescending()
    {
        var profile = CreateProfile();

        profile.AddEducation(
            "Université A",
            "Licence",
            new DateOnly(2018, 9, 1),
            new DateOnly(2021, 6, 30));

        profile.AddEducation(
            "Université B",
            "Master",
            new DateOnly(2022, 9, 1),
            new DateOnly(2024, 6, 30));

        _repository
            .GetWithEducationsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.Equal(2, result.Count);

        var educations = result.ToList();

        Assert.Equal("Université B", educations[0].InstitutionName);
        Assert.Equal("Université A", educations[1].InstitutionName);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoEducation()
    {
        var profile = CreateProfile();

        _repository
            .GetWithEducationsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetWithEducationsAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync());
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