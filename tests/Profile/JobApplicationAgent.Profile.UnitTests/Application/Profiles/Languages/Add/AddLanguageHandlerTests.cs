using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Add;

public sealed class AddLanguageHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddLanguageCommand> _validator;
    private readonly AddLanguageHandler _handler;

    public AddLanguageHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<AddLanguageCommand>>();

        _handler = new AddLanguageHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddLanguage_WhenProfileExists()
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
            command.ProficiencyLevel,
            result.ProficiencyLevel);

        var language = Assert.Single(profile.Languages);

        Assert.Equal(result.Id, language.Id);
        Assert.Equal(command.Name, language.Name);
        Assert.Equal(
            command.ProficiencyLevel,
            language.ProficiencyLevel);

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
            new AddLanguageCommandValidator();

        var handler = new AddLanguageHandler(
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

    private static AddLanguageCommand CreateCommand()
    {
        return new AddLanguageCommand(
            Name: "French",
            ProficiencyLevel: "Native");
    }
}