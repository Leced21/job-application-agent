using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Update;

public sealed class UpdateCandidateProfileHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldUpdateFieldsAndPreserveIdentityAndChildren()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var profile = new CandidateProfile("Old", "Name", "old@example.com", "123", "Developer", "Old summary");
        var link = profile.AddLink("Portfolio", "https://example.com");
        var preferences = profile.AddPreferences([], [], [], [], null, null, null);
        var id = profile.Id;
        var created = profile.CreatedAtUtc;
        repository.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(profile);
        var handler = new UpdateCandidateProfileHandler(repository, new UpdateCandidateProfileCommandValidator());
        using var cts = new CancellationTokenSource();
        var command = new UpdateCandidateProfileCommand("New", "Candidate", "new@example.com", null, null, null);
        var dto = await handler.HandleAsync(command, cts.Token);
        Assert.Equal(id, dto.Id);
        Assert.Equal(created, dto.CreatedAtUtc);
        Assert.Equal(command.FirstName, dto.FirstName);
        Assert.Equal(command.LastName, dto.LastName);
        Assert.Equal(command.Email, dto.Email);
        Assert.Null(dto.PhoneNumber);
        Assert.Null(dto.JobTitle);
        Assert.Null(dto.Summary);
        Assert.Same(link, Assert.Single(profile.Links));
        Assert.Same(preferences, profile.Preferences);
        Assert.Equal(profile.UpdatedAtUtc, dto.UpdatedAtUtc);
        await repository.Received(1).SaveChangesAsync(cts.Token);
        await repository.DidNotReceive().AddAsync(Arg.Any<CandidateProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowWhenProfileIsMissing()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new UpdateCandidateProfileHandler(repository, new UpdateCandidateProfileCommandValidator());
        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() =>
            handler.HandleAsync(new("Test", "Candidate", "test@example.com", null, null, null)));
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectInvalidCommandBeforeLoading()
    {
        var repository = Substitute.For<ICandidateProfileRepository>();
        var handler = new UpdateCandidateProfileHandler(repository, new UpdateCandidateProfileCommandValidator());
        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.HandleAsync(new("", "Candidate", "invalid", null, null, null)));
        await repository.DidNotReceive().GetForUpdateAsync(Arg.Any<CancellationToken>());
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
