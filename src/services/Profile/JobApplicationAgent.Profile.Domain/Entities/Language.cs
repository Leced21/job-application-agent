namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class Language
{
    private Language() { }

    public Language(
        Guid candidateProfileId,
        string name,
        string proficiencyLevel)
    {
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        Name = name;
        ProficiencyLevel = proficiencyLevel;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string ProficiencyLevel { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string proficiencyLevel)
    {
        Name = name;
        ProficiencyLevel = proficiencyLevel;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}