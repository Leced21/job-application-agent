namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class ProfessionalExperienceNotFoundException : Exception
{
    public ProfessionalExperienceNotFoundException(Guid experienceId)
        : base($"Professional experience '{experienceId}' does not exist.")
    {
    }
}