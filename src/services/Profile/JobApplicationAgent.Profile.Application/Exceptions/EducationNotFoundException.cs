namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class EducationNotFoundException : Exception
{
    public EducationNotFoundException(Guid educationId)
        : base($"Education '{educationId}' does not exist.")
    {
    }
}