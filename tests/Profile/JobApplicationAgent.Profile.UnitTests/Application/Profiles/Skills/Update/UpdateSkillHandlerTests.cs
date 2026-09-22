using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Skills.Update;

public sealed class UpdateSkillHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateSkillCommand> _validator;
    private readonly UpdateSkillHandler _handler;

    public UpdateSkillHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<UpdateSkillCommand>>();

        _handler =
            new UpdateSkillHandler(
                _repository,
                _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateSkill_WhenSkillExists()
    {
        var profile = CreateProfile();

        var skill = profile.AddSkill(
            "Databricks",
            "Data Engineering",
            "Intermediate",
            2);

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateSkillCommand(
            "Databricks",
            "Data Engineering",
            "Advanced",
            4);

        var result = await _handler.HandleAsync(
            skill.Id,
            command);

        Assert.Equal(skill.Id, result.Id);
        Assert.Equal("Databricks", result.Name);
        Assert.Equal("Data Engineering", result.Category);
        Assert.Equal("Advanced", result.Level);
        Assert.Equal(4, result.YearsOfExperience);

        await _repository
            .Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        var command = new UpdateSkillCommand(
            "Databricks",
            "Data Engineering",
            "Advanced",
            4);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository
            .DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenSkillDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateSkillCommand(
            "Databricks",
            "Data Engineering",
            "Advanced",
            4);

        await Assert.ThrowsAsync<SkillNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository
            .DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenCommandIsInvalid()
    {
        var repository =
            Substitute.For<ICandidateProfileRepository>();

        var validator =
            new UpdateSkillCommandValidator();

        var handler =
            new UpdateSkillHandler(
                repository,
                validator);

        var command = new UpdateSkillCommand(
            "",
            "Data Engineering",
            "Advanced",
            4);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await repository
            .DidNotReceive()
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>());

        await repository
            .DidNotReceive()
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