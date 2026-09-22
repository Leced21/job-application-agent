namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class Link
{
    private Link() { }

    public Link(
        Guid candidateProfileId,
        string name,
        string url)
    {
        Id = Guid.NewGuid();
        CandidateProfileId = candidateProfileId;
        Name = name;
        Url = url;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string name,
        string url)
    {
        Name = name;
        Url = url;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}