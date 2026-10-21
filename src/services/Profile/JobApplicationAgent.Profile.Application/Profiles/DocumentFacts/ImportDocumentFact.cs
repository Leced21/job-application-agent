using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.Application.Profiles.DocumentFacts;

public sealed record ImportedEvidenceField(string Field, string Value, int SegmentIndex, int Start, int Length, string Excerpt);
public sealed record ImportedProfileDates(DateOnly? StartDate, DateOnly? EndDate, bool? IsCurrent, DateOnly? IssueDate,
    DateOnly? ExpirationDate, bool Confirmed, string? ConfirmationNote, Guid ConfirmedByOwnerId, DateTime ConfirmedAtUtc);
public sealed record ImportDocumentFactCommand(Guid FactId, Guid OwnerId, Guid TargetProfileId, Guid ResumeId, Guid ExtractionId,
    string SourceSha256, string EngineVersion, string Kind, string Decision, DateTime ReviewedAtUtc, Guid ReviewedByOwnerId,
    ImportedEvidenceField[] Fields,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ImportedProfileDates? Dates = null);
public sealed record DocumentFactReceipt(Guid FactId, Guid OwnerId, Guid ProfileId, Guid SkillId, string SourceSha256, string Action, DateTime ImportedAtUtc,
    string Kind = "Skill", Guid EntityId = default)
{
    public static DocumentFactReceipt From(DocumentFactImport receipt) => new(receipt.FactId, receipt.OwnerId, receipt.ProfileId,
        receipt.Kind == "Skill" ? receipt.EntityId : Guid.Empty, receipt.SourceSha256, receipt.Action, receipt.ImportedAtUtc, receipt.Kind, receipt.EntityId);
}
public sealed record DocumentFactImportOutcome(DocumentFactReceipt Receipt, bool Created);
public sealed class DocumentFactImportConflictException(string message) : Exception(message);
public interface IDocumentFactImportRepository
{
    Task<DocumentFactImport?> GetAsync(Guid ownerId, Guid profileId, Guid factId, CancellationToken ct);
    Task<DocumentFactImportOutcome> ImportAsync(ImportDocumentFactCommand command, string hash, string json, CancellationToken ct);
}
public sealed class ImportDocumentFactHandler(IDocumentFactImportRepository repository)
{
    public async Task<DocumentFactImportOutcome> HandleAsync(Guid ownerId, Guid profileId, ImportDocumentFactCommand command, CancellationToken ct)
    {
        if (command.OwnerId != ownerId || command.TargetProfileId != profileId)
            throw new DocumentFactImportConflictException("The fact does not match the configured owner/profile binding.");
        if (command.FactId == Guid.Empty || command.ResumeId == Guid.Empty || command.ExtractionId == Guid.Empty ||
            command.ReviewedByOwnerId != ownerId || command.Decision != "Approved" ||
            command.ReviewedAtUtc.Kind != DateTimeKind.Utc || command.ReviewedAtUtc == default ||
            command.SourceSha256 is null || !System.Text.RegularExpressions.Regex.IsMatch(command.SourceSha256, "^[a-f0-9]{64}$") ||
            string.IsNullOrWhiteSpace(command.EngineVersion) || command.EngineVersion.Length > 100 ||
            command.Fields is not { Length: >= 1 and <= 5 } || command.Fields.Any(field => field is null ||
                string.IsNullOrWhiteSpace(field.Field) || string.IsNullOrWhiteSpace(field.Value) || field.Value.Length > 1000 || field.Length != field.Value.Length ||
                field.SegmentIndex < 1 || field.Start < 0 || field.Excerpt is null || field.Excerpt.Length > 1242 ||
                !field.Excerpt.Contains(field.Value, StringComparison.Ordinal)) ||
            command.Fields.Select(x => x.Field).Distinct(StringComparer.Ordinal).Count() != command.Fields.Length)
            throw new ValidationException("An import requires an approved owner-reviewed fact and bounded source evidence.");
        ValidateMapping(command);
        var json = JsonSerializer.Serialize(command);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return await repository.ImportAsync(command, hash, json, ct);
    }
    private static void ValidateMapping(ImportDocumentFactCommand command)
    {
        var fields = command.Fields.ToDictionary(x => x.Field, x => x.Value);
        bool Required(string key, int max) => fields.TryGetValue(key, out var value) && value.Length <= max;
        string[] allowed = command.Kind switch
        {
            "Skill" => ["Name"], "Experience" => ["Title", "Company", "Period", "Description"],
            "Education" => ["Degree", "Institution", "Period", "Description"],
            "Certification" => ["Name", "Issuer", "Period", "Identifier"], _ => []
        };
        var labelsValid = command.Kind switch
        {
            "Skill" => Required("Name", 150) && command.Dates is null,
            "Experience" => Required("Title", 150) && Required("Company", 200) && Required("Period", 1000),
            "Education" => Required("Degree", 150) && Required("Institution", 200) && Required("Period", 1000),
            "Certification" => Required("Name", 200) && Required("Issuer", 200) && Required("Period", 1000) &&
                (!fields.TryGetValue("Identifier", out var identifier) || identifier.Length <= 200), _ => false
        };
        if (!labelsValid || fields.Keys.Any(x => !allowed.Contains(x))) throw new ValidationException("Unsupported or incomplete source mapping.");
        if (command.Kind == "Skill") return;
        var dates = command.Dates;
        if (dates is null || !dates.Confirmed || dates.ConfirmedByOwnerId != command.OwnerId ||
            dates.ConfirmedAtUtc.Kind != DateTimeKind.Utc || dates.ConfirmedAtUtc < command.ReviewedAtUtc ||
            string.IsNullOrWhiteSpace(dates.ConfirmationNote) || dates.ConfirmationNote.Length > 1000)
            throw new ValidationException("Owner-confirmed dates and their audit are required.");
        bool Present(DateOnly? date) => date.HasValue && date.Value != DateOnly.MinValue;
        bool valid = command.Kind == "Certification"
            ? Present(dates.IssueDate) && dates.StartDate is null && dates.EndDate is null && dates.IsCurrent is null &&
                (dates.ExpirationDate is null || dates.ExpirationDate >= dates.IssueDate)
            : Present(dates.StartDate) && dates.IsCurrent.HasValue && dates.IssueDate is null && dates.ExpirationDate is null &&
                (dates.IsCurrent.Value ? dates.EndDate is null : Present(dates.EndDate) && dates.EndDate >= dates.StartDate);
        if (!valid) throw new ValidationException("Inconsistent or incomplete dates for the source category.");
    }
    public async Task<DocumentFactReceipt?> GetAsync(Guid ownerId, Guid profileId, Guid factId, CancellationToken ct)
    {
        var receipt = await repository.GetAsync(ownerId, profileId, factId, ct);
        return receipt is null ? null : DocumentFactReceipt.From(receipt);
    }
}
