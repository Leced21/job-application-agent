using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Languages.Delete;

public sealed class DeleteLanguageHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly DeleteLanguageHandler _handler;

    public DeleteLanguageHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new DeleteLanguageHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteLanguage_WhenLanguageExists()
    {
        var profile = CreateProfile();

        var language = profile.AddLanguage(
            "French",
            "Native");

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        await _handler.HandleAsync(language.Id);

        Assert.Empty(profile.Languages);

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
    public async Task HandleAsync_ShouldThrow_WhenLanguageDoesNotExist()
    {
        var profile = CreateProfile();

        _repository
            .GetForUpdateAsync(
                Arg.Any<CancellationToken>())
            .Returns(profile);

        await Assert.ThrowsAsync<LanguageNotFoundException>(
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