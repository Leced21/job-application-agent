using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Certifications.Update;

public sealed class UpdateCertificationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateCertificationCommand> _validator;
    private readonly UpdateCertificationHandler _handler;

    public UpdateCertificationHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _validator =
            Substitute.For<IValidator<UpdateCertificationCommand>>();

        _handler = new UpdateCertificationHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateCertification_WhenCertificationExists()
    {
        var profile = CreateProfile();

        var certification = profile.AddCertification(
            "Azure",
            "Google Cloud",
            new DateOnly(2025, 1, 1), null, null, null);

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateCertificationCommand(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        var result = await _handler.HandleAsync(
            certification.Id,
            command);

        Assert.Equal(certification.Id, result.Id);
        Assert.Equal("Azure", result.Name);
        Assert.Equal("Microsoft", result.IssuingOrganization);

        Assert.Equal("Azure", certification.Name);
        Assert.Equal(
            "Microsoft",
            certification.IssuingOrganization);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        var certificationId = Guid.NewGuid();

        var command = new UpdateCertificationCommand(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                certificationId,
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenCertificationDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var command = new UpdateCertificationCommand(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        await Assert.ThrowsAsync<CertificationNotFoundException>(
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
            new UpdateCertificationCommandValidator();

        var handler = new UpdateCertificationHandler(
            _repository,
            validator);

        var command = new UpdateCertificationCommand(
            "",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

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