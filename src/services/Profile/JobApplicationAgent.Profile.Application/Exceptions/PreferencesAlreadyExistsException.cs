namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class PreferencesAlreadyExistsException : Exception
{
    public PreferencesAlreadyExistsException() : base("Candidate preferences already exist.") { }
}
