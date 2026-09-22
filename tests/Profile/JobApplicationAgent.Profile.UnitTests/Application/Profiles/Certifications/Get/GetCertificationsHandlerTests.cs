using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Certifications.Get;

public sealed class GetCertificationsHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetCertificationsHandler _handler;

    public GetCertificationsHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new GetCertificationsHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCertificationsOrderedByName_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddCertification(
            "Google",
            "Google Cloud",
            new DateOnly(2025, 1, 1), null, null, null);

        profile.AddCertification(
            "AWS",
            "Amazon",
            new DateOnly(2025, 1, 1), null, null, null);

        profile.AddCertification(
            "Azure",
            "Microsoft",
            new DateOnly(2025, 1, 1), null, null, null);

        _repository
            .GetWithCertificationsAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync();

        Assert.Equal(3, result.Count);

        var certifications = result.ToList();

        Assert.Equal("AWS", certifications[0].Name);
        Assert.Equal("Azure", certifications[1].Name);
        Assert.Equal("Google", certifications[2].Name);

        Assert.Equal(
            "Amazon",
            certifications[0].IssuingOrganization);

        Assert.Equal(
            "Microsoft",
            certifications[1].IssuingOrganization);

        Assert.Equal(
            "Google Cloud",
            certifications[2].IssuingOrganization);

        await _repository.Received(1)
            .GetWithCertificationsAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoCertifications()
    {
        var profile = CreateProfile();

        _repository
            .GetWithCertificationsAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetWithCertificationsAsync(
                Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync());
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