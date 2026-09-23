using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Preferences.Add;

public sealed class AddPreferencesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreatePreferencesAndRejectDuplicateWithoutMutation()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var profile = new CandidateProfile("Test", "Candidate", "test@example.com");
        repository.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(profile);
        var handler = new AddPreferencesHandler(repository, new AddPreferencesCommandValidator());
        var command = new AddPreferencesCommand(["Developer"], ["Paris"], ["CDI"], ["Hybrid"], 50000, "EUR", new DateOnly(2027, 1, 1));
        using var cts = new CancellationTokenSource();
        var created = await handler.HandleAsync(command, cts.Token);
        Assert.Equal(profile.Id, created.CandidateProfileId);
        Assert.Equal(command.DesiredJobTitles, created.DesiredJobTitles);
        Assert.Equal(command.PreferredLocations, created.PreferredLocations);
        Assert.Equal(command.ContractTypes, created.ContractTypes);
        Assert.Equal(command.WorkModes, created.WorkModes);
        Assert.Equal(command.MinimumAnnualGrossSalary, created.MinimumAnnualGrossSalary);
        Assert.Equal(command.SalaryCurrency, created.SalaryCurrency);
        Assert.Equal(command.AvailableFrom, created.AvailableFrom);
        var entity = profile.Preferences;
        var timestamp = profile.UpdatedAtUtc;
        await Assert.ThrowsAsync<PreferencesAlreadyExistsException>(() =>
            handler.HandleAsync(new([], [], [], [], null, null, null), cts.Token));
        Assert.Same(entity, profile.Preferences);
        Assert.Equal(command.DesiredJobTitles, profile.Preferences!.DesiredJobTitles);
        Assert.Equal(timestamp, profile.UpdatedAtUtc);
        await repository.Received(1).SaveChangesAsync(cts.Token);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectMissingProfileWithoutSaving()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new AddPreferencesHandler(repository, new AddPreferencesCommandValidator());
        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() =>
            handler.HandleAsync(new([], [], [], [], null, null, null)));
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldValidateBeforeLoadingProfile()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new AddPreferencesHandler(repository, new AddPreferencesCommandValidator());
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new([], [], [], ["invalid"], null, null, null)));
        await repository.DidNotReceive().GetForUpdateAsync(Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
