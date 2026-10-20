namespace JobApplicationAgent.Documents.Domain;

public sealed class ResumeSeriesConflictException(string message) : Exception(message);
public sealed class ResumeSeries
{
    private ResumeSeries() { }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public static ResumeSeries Create(Guid ownerId, string name, DateTime now)
    {
        name = name.Trim();
        if (ownerId == Guid.Empty || name.Length is < 1 or > 200 || name.Any(char.IsControl) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A resume series requires an owner, a bounded name and a UTC date.");
        return new() { Id = Guid.NewGuid(), OwnerId = ownerId, Name = name,
            CreatedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
public sealed class ResumeSeriesVersion
{
    private ResumeSeriesVersion() { }
    public Guid ResumeId { get; private set; }
    public Guid SeriesId { get; private set; }
    public Guid OwnerId { get; private set; }
    public int VersionNumber { get; private set; }
    public string? Label { get; private set; }
    public DateTime AddedAtUtc { get; private set; }
    public static ResumeSeriesVersion Create(Guid ownerId, Guid seriesId, Guid resumeId, int number, string? label, DateTime now)
    {
        if (label?.Any(char.IsControl) == true) throw new ArgumentException("A version label cannot contain control characters.");
        label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        if (ownerId == Guid.Empty || seriesId == Guid.Empty || resumeId == Guid.Empty || number is < 1 or > 1000 ||
            label?.Length > 200 || label?.Any(char.IsControl) == true || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A version requires bounded metadata, valid identities and a UTC date.");
        return new() { OwnerId = ownerId, SeriesId = seriesId, ResumeId = resumeId, VersionNumber = number, Label = label,
            AddedAtUtc = new(now.Ticks - now.Ticks % 10, DateTimeKind.Utc) };
    }
}
