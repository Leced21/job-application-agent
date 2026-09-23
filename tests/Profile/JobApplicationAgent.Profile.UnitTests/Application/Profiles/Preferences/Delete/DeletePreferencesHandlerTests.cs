using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Preferences.Delete;

public sealed class DeletePreferencesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldRemovePreferencesAndSave()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        profile.AddPreferences([], [], [], [], null, null, null);
        repository.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(profile);
        await new DeletePreferencesHandler(repository).HandleAsync();
        Assert.Null(profile.Preferences);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_ShouldNotSaveWhenProfileOrPreferencesAreMissing(bool profileExists)
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        if (profileExists)
            repository.GetForUpdateAsync(Arg.Any<CancellationToken>())
                .Returns(new CandidateProfile("Test", "Candidate", "test@example.com"));
        var handler = new DeletePreferencesHandler(repository);
        if (profileExists)
            await Assert.ThrowsAsync<PreferencesNotFoundException>(() => handler.HandleAsync());
        else
            await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() => handler.HandleAsync());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
