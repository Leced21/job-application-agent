using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Links.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Links.Add;

public sealed class AddLinkHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddLinkCommand> _validator;
    private readonly AddLinkHandler _handler;

    public AddLinkHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<AddLinkCommand>>();

        _handler = new AddLinkHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddLink_WhenProfileExists()
    {
        var profile = CreateProfile();
        var command = CreateCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(command.Name, result.Name);
        Assert.Equal(
            command.Url,
            result.Url);

        var link = Assert.Single(profile.Links);

        Assert.Equal(result.Id, link.Id);
        Assert.Equal(command.Name, link.Name);
        Assert.Equal(
            command.Url,
            link.Url);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var command = CreateCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowValidationException_WhenCommandIsInvalid()
    {
        var validator =
            new AddLinkCommandValidator();

        var handler = new AddLinkHandler(
            _repository,
            validator);

        var command = CreateCommand() with
        {
            Name = string.Empty
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(command));

        await _repository.DidNotReceive()
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>());

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

    private static AddLinkCommand CreateCommand()
    {
        return new AddLinkCommand(
            Name: "French",
            Url: "https://example.com/native");
    }
}