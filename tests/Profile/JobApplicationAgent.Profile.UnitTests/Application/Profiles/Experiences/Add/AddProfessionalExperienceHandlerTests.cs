using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Experiences.Add;

public sealed class AddProfessionalExperienceHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<AddProfessionalExperienceCommand> _validator;
    private readonly AddProfessionalExperienceHandler _handler;

    public AddProfessionalExperienceHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _validator = Substitute.For<IValidator<AddProfessionalExperienceCommand>>();

        _handler = new AddProfessionalExperienceHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldAddProfessionalExperience_WhenCommandIsValid()
    {
        var profile = CreateProfile();
        var command = CreateValidCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync(command);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(command.CompanyName, result.CompanyName);
        Assert.Equal(command.JobTitle, result.JobTitle);
        Assert.Equal(command.Location, result.Location);
        Assert.Equal(command.StartDate, result.StartDate);
        Assert.Equal(command.EndDate, result.EndDate);
        Assert.Equal(command.IsCurrent, result.IsCurrent);
        Assert.Equal(command.Description, result.Description);

        Assert.Single(profile.ProfessionalExperiences);

        var experience = profile.ProfessionalExperiences.Single();

        Assert.Equal(result.Id, experience.Id);
        Assert.Equal(profile.Id, experience.CandidateProfileId);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task HandleAsync_ShouldThrowCandidateProfileNotFoundException_WhenProfileDoesNotExist()
    {
        var command = CreateValidCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(() => _handler.HandleAsync(command));

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static CandidateProfile CreateProfile()
    {
        return new CandidateProfile(
            firstName: "Test",
            lastName: "Candidate",
            email: "test@example.com",
            phoneNumber: "0612345678",
            jobTitle: "Data Engineer",
            summary: "Test profile");
    }

    private static AddProfessionalExperienceCommand CreateValidCommand()
    {
        return new AddProfessionalExperienceCommand(
            CompanyName: "Test Company",
            JobTitle: "Data Engineer",
            Location: "Paris",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: null,
            IsCurrent: true,
            Description: "Développement de pipelines de données.");
    }
}