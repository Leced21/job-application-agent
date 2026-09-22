namespace JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;

public sealed record AddCertificationCommand(
    string Name,
    string IssuingOrganization,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialId,
    string? CredentialUrl);