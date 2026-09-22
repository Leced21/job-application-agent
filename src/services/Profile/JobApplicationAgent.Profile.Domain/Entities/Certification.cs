namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class Certification
{
    private Certification() { }

    public Certification(Guid candidateProfileId,
        string name,
        string issuingOrganization,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? credentialId,
        string? credentialUrl)
    {
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        CreatedAtUtc = DateTime.UtcNow;
        Update(name, issuingOrganization, issueDate, expirationDate, credentialId, credentialUrl);
    }

    public Guid Id { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string IssuingOrganization { get; private set; } = string.Empty;
    public DateOnly IssueDate { get; private set; }
    public DateOnly? ExpirationDate { get; private set; }
    public string? CredentialId { get; private set; }
    public string? CredentialUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string issuingOrganization,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? credentialId,
        string? credentialUrl)
    {
        Name = name;
        IssuingOrganization = issuingOrganization;
        IssueDate = issueDate;
        ExpirationDate = expirationDate;
        CredentialId = credentialId;
        CredentialUrl = credentialUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
