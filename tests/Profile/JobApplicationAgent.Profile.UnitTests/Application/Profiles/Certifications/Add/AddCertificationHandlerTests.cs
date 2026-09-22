using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Certifications.Add;

public sealed class AddCertificationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddCertificationCommand> _validator;
    private readonly AddCertificationHandler _handler;

    public AddCertificationHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<AddCertificationCommand>>();

        _handler = new AddCertificationHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddCertification_WhenProfileExists()
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
            command.IssuingOrganization,
            result.IssuingOrganization);

        var certification = Assert.Single(profile.Certifications);

        Assert.Equal(result.Id, certification.Id);
        Assert.Equal(command.Name, certification.Name);
        Assert.Equal(
            command.IssuingOrganization,
            certification.IssuingOrganization);

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
            new AddCertificationCommandValidator();

        var handler = new AddCertificationHandler(
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

    private static AddCertificationCommand CreateCommand()
    {
        return new AddCertificationCommand(
            Name: "Azure",
            IssuingOrganization: "Microsoft",
            IssueDate: new DateOnly(2025, 1, 1), ExpirationDate: null, CredentialId: null, CredentialUrl: null);
    }
}