namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class ProfessionalExperience
{
    private ProfessionalExperience()
    {
    }

    public ProfessionalExperience(
        Guid candidateProfileId,
        string companyName,
        string jobTitle,
        DateOnly startDate,
        DateOnly? endDate = null,
        bool isCurrent = false,
        string? location = null,
        string? description = null)
    {
        Id = Guid.NewGuid();

        CandidateProfileId = candidateProfileId;

        CompanyName = companyName;
        JobTitle = jobTitle;
        Location = location;

        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;

        Description = description;

        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public void Update(
    string companyName,
    string jobTitle,
    DateOnly startDate,
    DateOnly? endDate = null,
    bool isCurrent = false,
    string? location = null,
    string? description = null)
    {
        CompanyName = companyName;
        JobTitle = jobTitle;
        Location = location;
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    public string CompanyName { get; private set; } = string.Empty;

    public string JobTitle { get; private set; } = string.Empty;

    public string? Location { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public bool IsCurrent { get; private set; }

    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
}