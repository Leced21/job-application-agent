using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Certifications.Delete;

public sealed class DeleteCertificationHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly DeleteCertificationHandler _handler;

    public DeleteCertificationHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new DeleteCertificationHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteCertification_WhenCertificationExists()
    {
        var profile = CreateProfile();

        var certification = profile.AddCertification(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        await _handler.HandleAsync(certification.Id);

        Assert.Empty(profile.Certifications);

        await _repository.Received(1)
            .SaveChangesAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

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

        await Assert.ThrowsAsync<CertificationNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

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