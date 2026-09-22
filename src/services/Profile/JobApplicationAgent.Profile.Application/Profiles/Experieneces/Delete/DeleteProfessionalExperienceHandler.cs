using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Experiences.Delete;

public sealed class DeleteProfessionalExperienceHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid experienceId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        var removed =
            profile.RemoveProfessionalExperience(experienceId);

        if (!removed)
        {
            throw new ProfessionalExperienceNotFoundException(
                experienceId);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}