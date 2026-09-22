using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Get;

public sealed class GetLanguagesHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetLanguagesHandler _handler;

    public GetLanguagesHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new GetLanguagesHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnLanguagesOrderedByName_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddLanguage(
            "Spanish",
            "Intermediate");

        profile.AddLanguage(
            "English",
            "Fluent");

        profile.AddLanguage(
            "French",
            "Native");

        _repository
            .GetWithLanguagesAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync();

        Assert.Equal(3, result.Count);

        var languages = result.ToList();

        Assert.Equal("English", languages[0].Name);
        Assert.Equal("French", languages[1].Name);
        Assert.Equal("Spanish", languages[2].Name);

        Assert.Equal(
            "Fluent",
            languages[0].ProficiencyLevel);

        Assert.Equal(
            "Native",
            languages[1].ProficiencyLevel);

        Assert.Equal(
            "Intermediate",
            languages[2].ProficiencyLevel);

        await _repository.Received(1)
            .GetWithLanguagesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoLanguages()
    {
        var profile = CreateProfile();

        _repository
            .GetWithLanguagesAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetWithLanguagesAsync(
                Arg.Any<CancellationToken>())
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