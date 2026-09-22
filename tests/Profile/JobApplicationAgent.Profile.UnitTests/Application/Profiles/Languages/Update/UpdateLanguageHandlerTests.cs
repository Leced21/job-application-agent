using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Update;

public sealed class UpdateLanguageHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateLanguageCommand> _validator;
    private readonly UpdateLanguageHandler _handler;

    public UpdateLanguageHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<UpdateLanguageCommand>>();

        _handler = new UpdateLanguageHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateLanguage_WhenLanguageExists()
    {
        var profile = CreateProfile();

        var language = profile.AddLanguage(
            "French",
            "Intermediate");

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateLanguageCommand(
            "French",
            "Native");

        var result = await _handler.HandleAsync(
            language.Id,
            command);

        Assert.Equal(language.Id, result.Id);
        Assert.Equal("French", result.Name);
        Assert.Equal("Native", result.ProficiencyLevel);

        Assert.Equal("French", language.Name);
        Assert.Equal(
            "Native",
            language.ProficiencyLevel);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var languageId = Guid.NewGuid();

        var command = new UpdateLanguageCommand(
            "French",
            "Native");

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                languageId,
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenLanguageDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateLanguageCommand(
            "French",
            "Native");

        await Assert.ThrowsAsync<LanguageNotFoundException>(
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
            new UpdateLanguageCommandValidator();

        var handler = new UpdateLanguageHandler(
            _repository,
            validator);

        var command = new UpdateLanguageCommand(
            "",
            "Native");

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