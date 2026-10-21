namespace JobApplicationAgent.Profile.Domain.Entities;

public sealed class DocumentFactImport
{
    private DocumentFactImport() { }
    public Guid FactId { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ProfileId { get; private set; }
    public Guid EntityId { get; private set; }
    public string Kind { get; private set; } = "Skill";
    public string SourceSha256 { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string RequestJson { get; private set; } = "";
    public DateTime ImportedAtUtc { get; private set; }
    public static DocumentFactImport Create(Guid factId, Guid ownerId, Guid profileId, Guid entityId, string sourceSha,
        string action, string hash, string json, DateTime now, string kind = "Skill") => new()
    {
        FactId = factId, OwnerId = ownerId, ProfileId = profileId, EntityId = entityId, Kind = kind, SourceSha256 = sourceSha,
        Action = action, RequestHash = hash, RequestJson = json,
        ImportedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc)
    };
}
