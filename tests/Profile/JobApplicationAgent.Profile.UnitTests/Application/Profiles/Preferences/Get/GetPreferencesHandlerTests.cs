using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Preferences.Get;

public sealed class GetPreferencesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnPreferencesWithoutExposingStoredArrays()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        profile.SetPreferences(["Developer"], ["Paris"], ["CDI"], ["Hybrid"], 50000, "EUR", null);
        repository.GetWithPreferencesAsync(Arg.Any<CancellationToken>()).Returns(profile);
        var result = await new GetPreferencesHandler(repository).HandleAsync();
        Assert.Equal(profile.Id, result.CandidateProfileId);
        Assert.Equal(profile.Preferences!.DesiredJobTitles, result.DesiredJobTitles);
        result.DesiredJobTitles[0] = "Changed";
        Assert.Equal("Developer", profile.Preferences.DesiredJobTitles[0]);
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenProfileIsMissing()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() =>
            new GetPreferencesHandler(repository).HandleAsync());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenPreferencesAreMissing()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        repository.GetWithPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(new CandidateProfile("Test", "Candidate", "test@example.com"));
        await Assert.ThrowsAsync<PreferencesNotFoundException>(() =>
            new GetPreferencesHandler(repository).HandleAsync());
    }
}
