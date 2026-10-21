namespace JobApplicationAgent.Documents.Domain;

public sealed class FactReviewConflictException(string message) : Exception(message);

public sealed class ResumeCandidateFact
{
    private ResumeCandidateFact() { }
    public Guid Id { get; private set; }
    public Guid ResumeId { get; private set; }
    public Guid ExtractionId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string SourceSha256 { get; private set; } = "";
    public string EngineVersion { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public string UnitKind { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string FieldsJson { get; private set; } = "";
    public string Decision { get; private set; } = "Pending";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByOwnerId { get; private set; }
    public string? ReviewNote { get; private set; }
    public string? ProfileDatesJson { get; private set; }
    public DateTime? ProfileDatesConfirmedAtUtc { get; private set; }
    public Guid? ProfileDatesConfirmedByOwnerId { get; private set; }

    public void ConfirmProfileDates(Guid ownerId, string datesJson, DateTime now)
    {
        if (ownerId != OwnerId || Decision != "Approved" || ReviewedByOwnerId != ownerId ||
            Kind is not ("Experience" or "Education" or "Certification") || now.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(datesJson) || datesJson.Length > 10000)
            throw new ArgumentException("Only the owner can confirm bounded profile dates for an approved supported fact.");
        if (ProfileDatesJson is not null)
        {
            // PostgreSQL JSONB normalizes property order and whitespace on round-trip.
            if (System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(ProfileDatesJson),
                System.Text.Json.Nodes.JsonNode.Parse(datesJson))) return;
            throw new FactReviewConflictException("The confirmed mapping is final. Create and review a new source fact to correct it.");
        }
        ProfileDatesJson = datesJson;
        ProfileDatesConfirmedByOwnerId = ownerId;
        ProfileDatesConfirmedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    public static ResumeCandidateFact Create(ResumeTextExtraction extraction, string kind, string unitKind,
        string fingerprint, string fieldsJson, DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || fingerprint.Length != 64 || string.IsNullOrWhiteSpace(fieldsJson))
            throw new ArgumentException("A fact requires evidence, fingerprint and a UTC date.");
        return new() { Id = Guid.NewGuid(), ResumeId = extraction.ResumeId, ExtractionId = extraction.Id,
            OwnerId = extraction.OwnerId, SourceSha256 = extraction.SourceSha256, EngineVersion = extraction.EngineVersion,
            Kind = kind, UnitKind = unitKind, Fingerprint = fingerprint, FieldsJson = fieldsJson,
            CreatedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
    public void Review(Guid ownerId, string decision, string? note, DateTime now)
    {
        if (ownerId != OwnerId || now.Kind != DateTimeKind.Utc || decision is not ("Approved" or "Rejected") || note?.Length > 1000)
            throw new ArgumentException("Invalid fact review.");
        if (Decision == decision && ReviewNote == note) return;
        if (Decision != "Pending") throw new FactReviewConflictException("The review is final; its decision and note cannot be changed.");
        Decision = decision;
        ReviewNote = note;
        ReviewedByOwnerId = ownerId;
        ReviewedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }
}
