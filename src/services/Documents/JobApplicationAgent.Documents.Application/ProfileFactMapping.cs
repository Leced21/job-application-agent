using System.Text.Json;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

// No date is derived from a year/month-only source. These values are explicit owner declarations.
public sealed record ConfirmProfileDatesCommand(DateOnly? StartDate, DateOnly? EndDate, bool? IsCurrent,
    DateOnly? IssueDate, DateOnly? ExpirationDate, bool Confirmed, string? ConfirmationNote);
public sealed record ProfileFactMappingDto(Guid FactId, string Kind, string SourceSha256, CandidateFactField[] Evidence,
    ConfirmProfileDatesCommand Dates, Guid ConfirmedByOwnerId, DateTime ConfirmedAtUtc,
    IReadOnlyDictionary<string, object?> TargetFields)
{
    public string DatesOrigin => "OwnerConfirmed";
}
public interface IProfileFactMappingRepository
{
    Task<ResumeCandidateFact?> ConfirmAsync(Guid ownerId, Guid resumeId, Guid factId, string datesJson, DateTime now, CancellationToken ct);
}
public sealed class ProfileFactMappingService(IResumeCandidateFactRepository facts, IProfileFactMappingRepository mappings, TimeProvider clock)
{
    public async Task<ProfileFactMappingDto?> ConfirmAsync(Guid ownerId, Guid resumeId, Guid factId, ConfirmProfileDatesCommand command, CancellationToken ct)
    {
        var fact = await facts.GetAsync(ownerId, resumeId, factId, ct);
        if (fact is null) return null;
        command = command with { ConfirmationNote = command.ConfirmationNote?.Trim() };
        Validate(fact, command);
        var saved = await mappings.ConfirmAsync(ownerId, resumeId, factId, JsonSerializer.Serialize(command), clock.GetUtcNow().UtcDateTime, ct);
        return saved is null ? null : From(saved);
    }
    public async Task<ProfileFactMappingDto?> GetAsync(Guid ownerId, Guid resumeId, Guid factId, CancellationToken ct)
    {
        var fact = await facts.GetAsync(ownerId, resumeId, factId, ct);
        return fact?.ProfileDatesJson is null ? null : From(fact);
    }
    public static void Validate(ResumeCandidateFact fact, ConfirmProfileDatesCommand command)
    {
        if (fact.Decision != "Approved" || fact.ReviewedByOwnerId != fact.OwnerId || fact.ReviewedAtUtc is null ||
            fact.Kind is not ("Experience" or "Education" or "Certification"))
            throw new ProfileFactImportRejectedException("An approved source fact of a supported dated category is required.");
        if (!command.Confirmed || string.IsNullOrWhiteSpace(command.ConfirmationNote) || command.ConfirmationNote.Length > 1000)
            throw new ProfileFactImportRejectedException("Explicit confirmation and a note explaining the verified dates are required.");
        var fields = CandidateFactDto.From(fact).Fields.ToDictionary(x => x.Field, x => x.Value);
        bool Required(string key, int length) => fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && value.Length <= length;
        var validFields = fact.Kind switch
        {
            "Experience" => Required("Title", 150) && Required("Company", 200),
            "Education" => Required("Degree", 150) && Required("Institution", 200),
            _ => Required("Name", 200) && Required("Issuer", 200) && (!fields.TryGetValue("Identifier", out var id) || id.Length <= 200)
        };
        if (!validFields || !Required("Period", 1000))
            throw new ProfileFactImportRejectedException("Required source labels and a period must be selected and approved before confirming a mapping.");
        bool Present(DateOnly? date) => date.HasValue && date.Value != DateOnly.MinValue;
        if (fact.Kind == "Certification")
        {
            if (!Present(command.IssueDate) || command.StartDate is not null || command.EndDate is not null || command.IsCurrent is not null ||
                command.ExpirationDate is { } expiration && expiration < command.IssueDate!.Value)
                throw new ProfileFactImportRejectedException("Certification requires an explicit issue date and an optional expiration on or after it; employment dates are unsupported.");
        }
        else if (!Present(command.StartDate) || command.IsCurrent is null || command.IssueDate is not null || command.ExpirationDate is not null ||
            command.IsCurrent == true && command.EndDate is not null || command.IsCurrent == false && !Present(command.EndDate) ||
            command.EndDate is { } end && end < command.StartDate!.Value)
            throw new ProfileFactImportRejectedException("Confirm a start date and either current status without an end, or a closed period with an end on or after the start.");
    }
    private static ProfileFactMappingDto From(ResumeCandidateFact fact)
    {
        var dates = JsonSerializer.Deserialize<ConfirmProfileDatesCommand>(fact.ProfileDatesJson!)!;
        var evidence = CandidateFactDto.From(fact).Fields;
        var fields = evidence.ToDictionary(x => x.Field, x => x.Value);
        var target = new Dictionary<string, object?>();
        if (fact.Kind == "Certification")
        {
            target["Name"] = fields["Name"]; target["IssuingOrganization"] = fields["Issuer"];
            target["IssueDate"] = dates.IssueDate; target["ExpirationDate"] = dates.ExpirationDate;
            target["CredentialId"] = fields.GetValueOrDefault("Identifier"); target["CredentialUrl"] = null;
        }
        else
        {
            target[fact.Kind == "Experience" ? "CompanyName" : "InstitutionName"] = fields[fact.Kind == "Experience" ? "Company" : "Institution"];
            target[fact.Kind == "Experience" ? "JobTitle" : "Degree"] = fields[fact.Kind == "Experience" ? "Title" : "Degree"];
            target["StartDate"] = dates.StartDate; target["EndDate"] = dates.EndDate; target["IsCurrent"] = dates.IsCurrent;
            target["Description"] = fields.GetValueOrDefault("Description"); target["Location"] = null;
            if (fact.Kind == "Education") target["FieldOfStudy"] = null;
        }
        return new(fact.Id, fact.Kind, fact.SourceSha256, evidence, dates, fact.ProfileDatesConfirmedByOwnerId!.Value,
            fact.ProfileDatesConfirmedAtUtc!.Value, target);
    }
}
