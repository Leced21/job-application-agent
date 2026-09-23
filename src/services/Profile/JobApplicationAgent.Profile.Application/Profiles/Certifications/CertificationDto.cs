namespace JobApplicationAgent.Profile.Application.Profiles.Certifications;

public sealed record CertificationDto(
    Guid Id,
    string Name,
    string IssuingOrganization,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialId,
    string? CredentialUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);