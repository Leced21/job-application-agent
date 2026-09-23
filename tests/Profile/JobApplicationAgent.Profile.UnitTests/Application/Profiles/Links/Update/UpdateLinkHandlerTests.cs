using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Links.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Links.Update;

public sealed class UpdateLinkHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateLinkCommand> _validator;
    private readonly UpdateLinkHandler _handler;

    public UpdateLinkHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<UpdateLinkCommand>>();

        _handler = new UpdateLinkHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateLink_WhenLinkExists()
    {
        var profile = CreateProfile();

        var link = profile.AddLink(
            "French",
            "https://example.com/intermediate");

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateLinkCommand(
            "French",
            "https://example.com/native");

        var result = await _handler.HandleAsync(
            link.Id,
            command);

        Assert.Equal(link.Id, result.Id);
        Assert.Equal("French", result.Name);
        Assert.Equal("https://example.com/native", result.Url);

        Assert.Equal("French", link.Name);
        Assert.Equal(
            "https://example.com/native",
            link.Url);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var linkId = Guid.NewGuid();

        var command = new UpdateLinkCommand(
            "French",
            "https://example.com/native");

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                linkId,
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenLinkDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateLinkCommand(
            "French",
            "https://example.com/native");

        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowValidationException_WhenCommandIsInvalid()
    {
        var validator =
            new UpdateLinkCommandValidator();

        var handler = new UpdateLinkHandler(
            _repository,
            validator);

        var command = new UpdateLinkCommand(
            "",
            "https://example.com/native");

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(
                Guid.NewGuid(),
                command));

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
}