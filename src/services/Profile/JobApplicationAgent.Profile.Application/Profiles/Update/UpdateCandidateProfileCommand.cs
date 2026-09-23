namespace JobApplicationAgent.Profile.Application.Profiles.Update
{
    public sealed record UpdateCandidateProfileCommand(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? JobTitle,
        string? Summary
    );
}
