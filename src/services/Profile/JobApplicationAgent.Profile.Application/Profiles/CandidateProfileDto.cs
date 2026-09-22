namespace JobApplicationAgent.Profile.Application.Profiles
{
    public sealed record CandidateProfileDto(
        Guid Id,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? JobTitle,
        string? Summary,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc
        
    );
}
