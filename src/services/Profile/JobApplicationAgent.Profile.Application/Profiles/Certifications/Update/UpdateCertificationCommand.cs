namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Update;

public sealed record UpdateCertificationCommand(
    string Name,
    string IssuingOrganization,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialId,
    string? CredentialUrl);