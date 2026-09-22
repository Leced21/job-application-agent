using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Delete;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Skills.Delete;

public sealed class DeleteSkillHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly DeleteSkillHandler _handler;

    public DeleteSkillHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new DeleteSkillHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteSkill_WhenSkillExists()
    {
        var profile = CreateProfile();

        var skill = profile.AddSkill(
            "Databricks",
            "Data Engineering",
            "Advanced",
            4);

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        await _handler.HandleAsync(skill.Id);

        Assert.Empty(profile.Skills);

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

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

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

        await Assert.ThrowsAsync<SkillNotFoundException>(
            () => _handler.HandleAsync(Guid.NewGuid()));

        await _repository
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