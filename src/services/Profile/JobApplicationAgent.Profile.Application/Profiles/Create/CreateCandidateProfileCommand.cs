namespace JobApplicationAgent.Profile.Application.Profiles.Create
{
    public sealed record CreateCandidateProfileCommand(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? JobTitle,
        string? Summary
    );
}
