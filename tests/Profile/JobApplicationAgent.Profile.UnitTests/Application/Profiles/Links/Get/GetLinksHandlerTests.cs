using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Links.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Links.Get;

public sealed class GetLinksHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetLinksHandler _handler;

    public GetLinksHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new GetLinksHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnLinksOrderedByName_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddLink(
            "Portfolio",
            "https://example.com/portfolio");

        profile.AddLink(
            "GitHub",
            "https://github.com/example");

        profile.AddLink(
            "LinkedIn",
            "https://www.linkedin.com/in/example");

        _repository
            .GetWithLinksAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        var result =
            await _handler.HandleAsync();

        Assert.Equal(3, result.Count);

        var links = result.ToList();

        Assert.Equal("GitHub", links[0].Name);
        Assert.Equal("LinkedIn", links[1].Name);
        Assert.Equal("Portfolio", links[2].Name);

        Assert.Equal(
            "https://github.com/example",
            links[0].Url);

        Assert.Equal(
            "https://www.linkedin.com/in/example",
            links[1].Url);

        Assert.Equal(
            "https://example.com/portfolio",
            links[2].Url);

        await _repository.Received(1)
            .GetWithLinksAsync(
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoLinks()
    {
        var profile = CreateProfile();

        _repository
            .GetWithLinksAsync(
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
            .GetWithLinksAsync(
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