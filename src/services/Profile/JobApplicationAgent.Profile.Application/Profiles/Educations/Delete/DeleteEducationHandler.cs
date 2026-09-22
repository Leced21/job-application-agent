using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Application.Exceptions;

namespace JobApplicationAgent.Profile.Application.Profiles.Educations.Delete;

public sealed class DeleteEducationHandler(
    ICandidateProfileRepository repository)
{
    public async Task HandleAsync(
        Guid educationId,
        CancellationToken cancellationToken = default)
    {
        var profile =
            await repository.GetForUpdateAsync(cancellationToken);

        if (profile is null)
        {
            throw new CandidateProfileNotFoundException();
        }

        var removed =
            profile.RemoveEducation(educationId);

        if (!removed)
        {
            throw new EducationNotFoundException(
                educationId);
        }

        await repository.SaveChangesAsync(
            cancellationToken);
    }
}