namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class CandidateProfileNotFoundException: Exception
{
    public CandidateProfileNotFoundException(): base("Candidate profile does not exist.")
    {
    }
}