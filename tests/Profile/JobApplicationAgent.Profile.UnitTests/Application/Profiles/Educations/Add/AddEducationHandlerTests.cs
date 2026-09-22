using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Educations.Add;

public sealed class AddEducationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddEducationCommand> _validator;
    private readonly AddEducationHandler _handler;

    public AddEducationHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _validator = Substitute.For<IValidator<AddEducationCommand>>();

        _handler = new AddEducationHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddEducation_WhenCommandIsValid()
    {
        var profile = CreateProfile();
        var command = CreateCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync(command);

        Assert.NotNull(result);
        Assert.Equal(command.InstitutionName, result.InstitutionName);
        Assert.Equal(command.Degree, result.Degree);
        Assert.Equal(command.FieldOfStudy, result.FieldOfStudy);
        Assert.Equal(command.Location, result.Location);
        Assert.Equal(command.StartDate, result.StartDate);
        Assert.Equal(command.EndDate, result.EndDate);
        Assert.Equal(command.IsCurrent, result.IsCurrent);
        Assert.Equal(command.Description, result.Description);

        Assert.Single(profile.Educations);

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
            () => _handler.HandleAsync(command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowValidationException_WhenCommandIsInvalid()
    {
        var validator = new AddEducationCommandValidator();

        var handler = new AddEducationHandler(
            _repository,
            validator);

        var command = CreateCommand() with
        {
            InstitutionName = string.Empty
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.HandleAsync(command));

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

    private static AddEducationCommand CreateCommand()
    {
        return new AddEducationCommand(
            InstitutionName: "Université Paris-Saclay",
            Degree: "Master",
            FieldOfStudy: "Data Science",
            Location: "Paris",
            StartDate: new DateOnly(2022, 9, 1),
            EndDate: new DateOnly(2024, 6, 30),
            IsCurrent: false,
            Description: "Master spécialisé en Data Science.");
    }
}