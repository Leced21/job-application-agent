namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class Skill
{
    private Skill()
    {
    }

    public Skill(
        Guid candidateProfileId,
        string name,
        string? category = null,
        string? level = null,
        int? yearsOfExperience = null)
    {
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        Name = name;
        Category = category;
        Level = level;
        YearsOfExperience = yearsOfExperience;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
    public void Update(
        string name,
        string? category = null,
        string? level = null,
        int? yearsOfExperience = null)
    {
        Name = name;
        Category = category;
        Level = level;
        YearsOfExperience = yearsOfExperience;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    public string? Level { get; private set; }

    public int? YearsOfExperience { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
}