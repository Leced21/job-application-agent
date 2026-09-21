using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Educations.Update;

public sealed class UpdateEducationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateEducationCommand> _validator;
    private readonly UpdateEducationHandler _handler;

    public UpdateEducationHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _validator = Substitute.For<IValidator<UpdateEducationCommand>>();

        _handler = new UpdateEducationHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateEducation_WhenEducationExists()
    {
        var profile = CreateProfile();

        var education = profile.AddEducation(
            "Université A",
            "Licence",
            new DateOnly(2018, 9, 1),
            new DateOnly(2021, 6, 30),
            false,
            "Informatique",
            "Paris",
            "Ancienne formation");

        var command = CreateCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync(
            education.Id,
            command);

        Assert.Equal(education.Id, result.Id);
        Assert.Equal(command.InstitutionName, result.InstitutionName);
        Assert.Equal(command.Degree, result.Degree);
        Assert.Equal(command.FieldOfStudy, result.FieldOfStudy);
        Assert.Equal(command.Location, result.Location);
        Assert.Equal(command.StartDate, result.StartDate);
        Assert.Equal(command.EndDate, result.EndDate);
        Assert.Equal(command.IsCurrent, result.IsCurrent);
        Assert.Equal(command.Description, result.Description);

        await _repository.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var command = CreateCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenEducationDoesNotExist()
    {
        var profile = CreateProfile();
        var command = CreateCommand();
        var educationId = Guid.NewGuid();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var exception =
            await Assert.ThrowsAsync<EducationNotFoundException>(
                () => _handler.HandleAsync(
                    educationId,
                    command));

        Assert.Contains(
            educationId.ToString(),
            exception.Message);

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowValidationException_WhenCommandIsInvalid()
    {
        var validator = new UpdateEducationCommandValidator();

        var handler = new UpdateEducationHandler(
            _repository,
            validator);

        var command = CreateCommand() with
        {
            InstitutionName = string.Empty
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository.DidNotReceive()
            .GetForUpdateAsync(Arg.Any<CancellationToken>());

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
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

    private static UpdateEducationCommand CreateCommand()
    {
        return new UpdateEducationCommand(
            InstitutionName: "Université Paris-Saclay",
            Degree: "Master",
            FieldOfStudy: "Data Science",
            Location: "Paris",
            StartDate: new DateOnly(2022, 9, 1),
            EndDate: new DateOnly(2024, 6, 30),
            IsCurrent: false,
            Description: "Formation mise à jour.");
    }
}