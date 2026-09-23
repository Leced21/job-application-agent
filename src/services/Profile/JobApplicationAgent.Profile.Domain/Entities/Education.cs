namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class Education
{
    private Education()
    {
    }

    public Education(
        Guid candidateProfileId,
        string institutionName,
        string degree,
        DateOnly startDate,
        DateOnly? endDate = null,
        bool isCurrent = false,
        string? fieldOfStudy = null,
        string? location = null,
        string? description = null)
    {
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        InstitutionName = institutionName;
        Degree = degree;
        FieldOfStudy = fieldOfStudy;
        Location = location;
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CandidateProfileId { get; private set; }
    public CandidateProfile CandidateProfile { get; private set; } = null!;

    public string InstitutionName { get; private set; } = string.Empty;
    public string Degree { get; private set; } = string.Empty;
    public string? FieldOfStudy { get; private set; }
    public string? Location { get; private set; }

    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsCurrent { get; private set; }

    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string institutionName,
        string degree,
        DateOnly startDate,
        DateOnly? endDate = null,
        bool isCurrent = false,
        string? fieldOfStudy = null,
        string? location = null,
        string? description = null)
    {
        InstitutionName = institutionName;
        Degree = degree;
        FieldOfStudy = fieldOfStudy;
        Location = location;
        StartDate = startDate;
        EndDate = endDate;
        IsCurrent = isCurrent;
        Description = description;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}