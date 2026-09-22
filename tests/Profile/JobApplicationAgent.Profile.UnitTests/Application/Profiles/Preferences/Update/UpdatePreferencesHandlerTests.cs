using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Preferences.Update;

public sealed class UpdatePreferencesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateThenUpdateSamePreferences()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        repository.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(profile);
        var handler = new UpdatePreferencesHandler(repository, new UpdatePreferencesCommandValidator());
        var command = new UpdatePreferencesCommand(["Developer"], ["Paris"], ["CDI"], ["Hybrid"], 50000, "EUR", null);
        using var cts = new CancellationTokenSource();
        var created = await handler.HandleAsync(command, cts.Token);
        var entity = profile.Preferences;
        Assert.NotNull(entity);
        Assert.Equal(profile.Id, created.CandidateProfileId);
        Assert.Equal(command.DesiredJobTitles, created.DesiredJobTitles);
        Assert.Equal(command.PreferredLocations, created.PreferredLocations);
        Assert.Equal(command.ContractTypes, created.ContractTypes);
        Assert.Equal(command.WorkModes, created.WorkModes);
        Assert.Equal(command.MinimumAnnualGrossSalary, created.MinimumAnnualGrossSalary);
        Assert.Equal("EUR", created.SalaryCurrency);

        var updated = await handler.HandleAsync(new([], [], [], ["Remote"], null, null, new DateOnly(2027, 1, 1)), cts.Token);
        Assert.Same(entity, profile.Preferences);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Empty(updated.DesiredJobTitles);
        Assert.Null(updated.MinimumAnnualGrossSalary);
        Assert.Null(updated.SalaryCurrency);
        Assert.Equal(new DateOnly(2027, 1, 1), updated.AvailableFrom);
        await repository.Received(2).SaveChangesAsync(cts.Token);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectMissingProfileWithoutSaving()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new UpdatePreferencesHandler(repository, new UpdatePreferencesCommandValidator());
        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() =>
            handler.HandleAsync(new([], [], [], [], null, null, null)));
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldValidateBeforeLoadingProfile()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new UpdatePreferencesHandler(repository, new UpdatePreferencesCommandValidator());
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new([], [], [], ["invalid"], null, null, null)));
        await repository.DidNotReceive().GetForUpdateAsync(Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
