using FluentValidation;
using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Update;
using JobApplicationAgent.Profile.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Profile.UnitTests.Application.Profiles.Experiences.Update;

public sealed class UpdateProfessionalExperienceHandlerTests
{
    private readonly ICandidateProfileRepository _repository;
    private readonly IValidator<UpdateProfessionalExperienceCommand> _validator;
    private readonly UpdateProfessionalExperienceHandler _handler;

    public UpdateProfessionalExperienceHandlerTests()
    {
        _repository = Substitute.For<ICandidateProfileRepository>();
        _validator =
            Substitute.For<IValidator<UpdateProfessionalExperienceCommand>>();

        _handler = new UpdateProfessionalExperienceHandler(
            _repository,
            _validator);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateProfessionalExperience_WhenExperienceExists()
    {
        var profile = CreateProfile();

        var experience = profile.AddProfessionalExperience(
            companyName: "Old Company",
            jobTitle: "Data Engineer",
            startDate: new DateOnly(2022, 1, 1),
            endDate: new DateOnly(2023, 12, 31),
            isCurrent: false,
            location: "Lyon",
            description: "Old description");

        var command = CreateValidCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        var result = await _handler.HandleAsync(
            experience.Id,
            command);

        Assert.Equal(experience.Id, result.Id);
        Assert.Equal(command.CompanyName, result.CompanyName);
        Assert.Equal(command.JobTitle, result.JobTitle);
        Assert.Equal(command.Location, result.Location);
        Assert.Equal(command.StartDate, result.StartDate);
        Assert.Equal(command.EndDate, result.EndDate);
        Assert.Equal(command.IsCurrent, result.IsCurrent);
        Assert.Equal(command.Description, result.Description);

        Assert.Equal(command.CompanyName, experience.CompanyName);
        Assert.Equal(command.JobTitle, experience.JobTitle);
        Assert.Equal(command.Location, experience.Location);

        await _repository.Received(1)
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowCandidateProfileNotFoundException_WhenProfileDoesNotExist()
    {
        var command = CreateValidCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns((CandidateProfile?)null);

        await Assert.ThrowsAsync<CandidateProfileNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowProfessionalExperienceNotFoundException_WhenExperienceDoesNotExist()
    {
        var profile = CreateProfile();
        var command = CreateValidCommand();

        _repository
            .GetForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(profile);

        await Assert.ThrowsAsync<ProfessionalExperienceNotFoundException>(
            () => _handler.HandleAsync(
                Guid.NewGuid(),
                command));

        await _repository.DidNotReceive()
            .SaveChangesAsync(Arg.Any<CancellationToken>());
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

    private static UpdateProfessionalExperienceCommand CreateValidCommand()
    {
        return new UpdateProfessionalExperienceCommand(
            CompanyName: "New Company",
            JobTitle: "Senior Data Engineer",
            Location: "Paris",
            StartDate: new DateOnly(2024, 1, 1),
            EndDate: null,
            IsCurrent: true,
            Description: "Updated description");
    }
}