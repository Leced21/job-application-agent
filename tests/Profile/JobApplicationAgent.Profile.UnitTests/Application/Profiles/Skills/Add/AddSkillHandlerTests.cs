using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Skills.Add;

public sealed class AddSkillHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddSkillCommand> _validator;
    private readonly AddSkillHandler _handler;

    public AddSkillHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<AddSkillCommand>>();

        _handler = new AddSkillHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddSkill_WhenProfileExists()
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
        Assert.Equal(command.Category, result.Category);
        Assert.Equal(command.Level, result.Level);
        Assert.Equal(
            command.YearsOfExperience,
            result.YearsOfExperience);

        var skill = Assert.Single(profile.Skills);

        Assert.Equal(result.Id, skill.Id);
        Assert.Equal(command.Name, skill.Name);

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
        var validator = new AddSkillCommandValidator();

        var handler = new AddSkillHandler(
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

    private static AddSkillCommand CreateCommand()
    {
        return new AddSkillCommand(
            Name: "Databricks",
            Category: "Data Engineering",
            Level: "Advanced",
            YearsOfExperience: 4);
    }
}