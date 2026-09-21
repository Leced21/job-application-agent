namespace JobApplicationAgent.Profile.Application.Exceptions
{
    public sealed class CandidateProfileAlreadyExistsException : Exception
    {
        public CandidateProfileAlreadyExistsException() : base("A candidate profile already exists.")
        {

        }
    }
}
