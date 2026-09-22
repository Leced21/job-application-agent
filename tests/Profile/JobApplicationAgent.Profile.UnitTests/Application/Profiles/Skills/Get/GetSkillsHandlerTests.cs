using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Get;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Skills.Get;

public sealed class GetSkillsHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly GetSkillsHandler _handler;

    public GetSkillsHandlerTests()
    {
        _repository =
            Substitute.For<ICandidateProfileRepository>();

        _handler =
            new GetSkillsHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSkills_WhenProfileExists()
    {
        var profile = CreateProfile();

        profile.AddSkill(
            "Databricks",
            "Data Engineering",
            "Advanced",
            4);

        _repository
            .GetWithSkillsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        var skill = Assert.Single(result);

        Assert.Equal("Databricks", skill.Name);
        Assert.Equal("Data Engineering", skill.Category);
        Assert.Equal("Advanced", skill.Level);
        Assert.Equal(4, skill.YearsOfExperience);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSkillsOrderedByName()
    {
        var profile = CreateProfile();

        profile.AddSkill("Python");
        profile.AddSkill("Azure");
        profile.AddSkill("Databricks");

        _repository
            .GetWithSkillsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.Equal(
            new[] { "Azure", "Databricks", "Python" },
            result.Select(x => x.Name));
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyCollection_WhenProfileHasNoSkills()
    {
        var profile = CreateProfile();

        _repository
            .GetWithSkillsAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProfileDoesNotExist()
    {
        _repository
            .GetWithSkillsAsync(Arg.Any<CancellationToken>())
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