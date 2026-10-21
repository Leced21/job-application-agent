using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record CompositionEvidenceReference(Guid FactId, string Field, int SegmentIndex, int Start, int Length);
public sealed record CompositionBlock(Guid FactId, string Kind, string Text, string DatesOrigin, CompositionEvidenceReference[] Evidence);
public sealed record ComposedDocumentContent(string Title, string PlainText, CompositionBlock[] Blocks, string[] Notices);
public sealed record CompositionReviewCommand(string ContentSha256, string Decision, bool Confirmed, string? Note = null);
public sealed record CompositionSummary(Guid DraftId, string Kind, string TemplateVersion, string SourceSnapshotSha256,
    string ContentSha256, DateTime CreatedAtUtc, string Decision, Guid? ReviewedByOwnerId, DateTime? ReviewedAtUtc, string? ReviewNote)
{
    public static CompositionSummary From(DocumentComposition composition) => new(composition.DraftId, composition.Kind,
        composition.TemplateVersion, composition.SourceSnapshotSha256, composition.ContentSha256, composition.CreatedAtUtc,
        composition.Decision, composition.ReviewedByOwnerId, composition.ReviewedAtUtc, composition.ReviewNote);
}
public sealed record CompositionDto(CompositionSummary Composition, ComposedDocumentContent Content)
{
    public static CompositionDto From(DocumentComposition composition) => new(CompositionSummary.From(composition),
        JsonSerializer.Deserialize<ComposedDocumentContent>(composition.ContentJson)!);
}
public sealed record CompositionWriteResult(CompositionDto Document, bool Created);
public interface IDocumentCompositionRepository
{
    Task<DocumentComposition?> GetAsync(Guid ownerId, Guid draftId, CancellationToken ct);
    Task<(DocumentComposition Composition, bool Created)> SaveOrGetAsync(DocumentComposition composition, CancellationToken ct);
    Task<DocumentComposition?> ReviewAsync(Guid ownerId, Guid draftId, CompositionReviewCommand command, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<CompositionSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct);
}
public static class VerifiedDocumentComposer
{
    public const string Version = "verified-fr-v1";
    public static ComposedDocumentContent Compose(PreparedDocumentDraft draft)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(draft.SnapshotJson))) != draft.SnapshotSha256)
            throw new DraftRejectedException("The stored source snapshot does not match its hash.");
        DraftSourceSnapshot source;
        try { source = JsonSerializer.Deserialize<DraftSourceSnapshot>(draft.SnapshotJson)!; }
        catch (JsonException) { throw new DraftRejectedException("Unsupported source snapshot."); }
        if (source is null || source.SchemaVersion != "prepared-draft-v1" || source.Job is null || source.Job.Id != draft.JobId ||
            string.IsNullOrWhiteSpace(source.Job.Title) || string.IsNullOrWhiteSpace(source.Job.CompanyName) ||
            source.Candidate?.Resume is null || source.Candidate.Resume.Id != draft.ResumeId || source.Candidate.Facts is not { Length: >= 1 and <= 50 })
            throw new DraftRejectedException("A complete, supported frozen source snapshot is required.");
        var blocks = source.Candidate.Facts.Select(item => Block(item, draft, source.Candidate.Resume.Sha256)).ToArray();
        if (blocks.Select(x => x.FactId).Distinct().Count() != blocks.Length) throw new DraftRejectedException("Duplicate source facts.");
        var title = $"Candidature au poste : {source.Job.Title} — {source.Job.CompanyName}";
        var body = string.Join("\n\n", blocks.Select(x => x.Text));
        var text = draft.Kind == "Resume"
            ? $"{title}\n\nÉléments du parcours sélectionnés :\n\n{body}"
            : $"{title}\n\nMadame, Monsieur,\n\nJe vous présente ma candidature au poste de {source.Job.Title} auprès de {source.Job.CompanyName}.\n\nVoici les éléments de mon parcours que je souhaite porter à votre attention :\n\n{body}\n\nJe reste à votre disposition pour un échange.\n\nCordialement,";
        return new(title, text, blocks, ["Identité et coordonnées absentes de ces sources : elles ne sont pas ajoutées automatiquement.",
            "Les libellés et descriptions sont repris tels qu'approuvés ; contrôler leur sens et leur contexte avant validation.",
            "Cette composition ne constitue pas une vérification externe des qualifications ni de la disponibilité de l'offre."]);
    }
    private static CompositionBlock Block(DraftFactSnapshot item, PreparedDocumentDraft draft, string resumeSha)
    {
        var fact = item?.Fact;
        if (fact is null || fact.Id == Guid.Empty || fact.ResumeId != draft.ResumeId || fact.SourceSha256 != resumeSha ||
            fact.Decision != "Approved" || fact.ReviewedByOwnerId != draft.OwnerId || fact.ReviewedAtUtc is null || fact.Fields is not { Length: >= 1 and <= 4 } ||
            fact.Fields.Any(x => x is null || string.IsNullOrWhiteSpace(x.Field) || string.IsNullOrWhiteSpace(x.Value) || x.Value.Length > 1000 ||
                x.Length != x.Value.Length || x.SegmentIndex < 1 || x.Start < 0 || x.Excerpt is null || !x.Excerpt.Contains(x.Value, StringComparison.Ordinal)) ||
            fact.Fields.Select(x => x.Field).Distinct().Count() != fact.Fields.Length)
            throw new DraftRejectedException("Only approved, bounded facts from this frozen CV can be composed.");
        var schema = CandidateFactBuilder.Schemas.SingleOrDefault(x => x.Kind == fact.Kind);
        if (schema is null || fact.Fields.Any(x => !schema.AllowedFields.Contains(x.Field)) || !fact.Fields.Any(x => x.Field == schema.RequiredField))
            throw new DraftRejectedException("Unsupported fact schema in the frozen sources.");
        var heading = fact.Kind switch { "Skill" => "Compétence", "Experience" => "Expérience", "Education" => "Formation", _ => "Certification" };
        string Label(string field) => field switch { "Name" => "Nom", "Title" => "Intitulé", "Company" => "Entreprise", "Period" => "Période indiquée dans le CV",
            "Description" => "Description", "Degree" => "Diplôme", "Institution" => "Établissement", "Issuer" => "Organisme", "Identifier" => "Identifiant", _ => field };
        var byField = fact.Fields.ToDictionary(x => x.Field);
        var lines = schema.AllowedFields.Where(byField.ContainsKey).Select(key => $"{Label(key)} : {byField[key].Value}").ToList();
        if (item!.Dates is { } dates)
        {
            bool Present(DateOnly? date) => date.HasValue && date.Value != DateOnly.MinValue;
            var valid = fact.Kind == "Certification"
                ? Present(dates.IssueDate) && dates.StartDate is null && dates.EndDate is null && dates.IsCurrent is null && (dates.ExpirationDate is null || dates.ExpirationDate >= dates.IssueDate)
                : fact.Kind is "Experience" or "Education" && Present(dates.StartDate) && dates.IsCurrent.HasValue && dates.IssueDate is null && dates.ExpirationDate is null &&
                    (dates.IsCurrent.Value ? dates.EndDate is null : Present(dates.EndDate) && dates.EndDate >= dates.StartDate);
            if (!dates.Confirmed || item.DatesConfirmedByOwnerId != draft.OwnerId || item.DatesConfirmedAtUtc is null ||
                item.DatesConfirmedAtUtc < fact.ReviewedAtUtc || !valid)
                throw new DraftRejectedException("Invalid date confirmation in the frozen sources.");
            string Date(DateOnly? date) => date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lines.Add(fact.Kind == "Certification" ? $"Dates confirmées par le candidat : émission {Date(dates.IssueDate)}" +
                (dates.ExpirationDate is null ? "" : $", expiration {Date(dates.ExpirationDate)}") :
                $"Dates confirmées par le candidat : {Date(dates.StartDate)} — {(dates.IsCurrent == true ? "en cours" : Date(dates.EndDate))}");
        }
        return new(fact.Id, fact.Kind, heading + "\n" + string.Join("\n", lines), item.DatesOrigin,
            fact.Fields.Select(x => new CompositionEvidenceReference(fact.Id, x.Field, x.SegmentIndex, x.Start, x.Length)).ToArray());
    }
}
public sealed class DocumentCompositionService(IPreparedDraftRepository drafts, IDocumentCompositionRepository compositions, TimeProvider clock)
{
    public async Task<CompositionWriteResult?> ComposeAsync(Guid ownerId, Guid draftId, CancellationToken ct)
    {
        var existing = await compositions.GetAsync(ownerId, draftId, ct);
        if (existing is not null) return new(CompositionDto.From(existing), false);
        var draft = await drafts.GetAsync(ownerId, draftId, ct);
        if (draft is null) return null;
        var json = JsonSerializer.Serialize(VerifiedDocumentComposer.Compose(draft));
        if (Encoding.UTF8.GetByteCount(json) > 2 * 1024 * 1024) throw new DraftRejectedException("Composed content exceeds 2 MiB; prepare a smaller source selection.");
        var composition = DocumentComposition.Create(ownerId, draftId, draft.Kind, VerifiedDocumentComposer.Version, draft.SnapshotSha256, json, clock.GetUtcNow().UtcDateTime);
        var saved = await compositions.SaveOrGetAsync(composition, ct);
        return new(CompositionDto.From(saved.Composition), saved.Created);
    }
    public async Task<CompositionDto?> GetAsync(Guid ownerId, Guid draftId, CancellationToken ct)
    { var value = await compositions.GetAsync(ownerId, draftId, ct); return value is null ? null : CompositionDto.From(value); }
    public async Task<CompositionDto?> ReviewAsync(Guid ownerId, Guid draftId, CompositionReviewCommand command, CancellationToken ct)
    {
        if (!command.Confirmed || command.Decision is not ("Approved" or "Rejected") || command.Note?.Length > 1000 ||
            command.ContentSha256 is null || !System.Text.RegularExpressions.Regex.IsMatch(command.ContentSha256, "^[a-f0-9]{64}$"))
            throw new InvalidResumeException("Review requires explicit confirmation, the exact SHA-256, Approved/Rejected and a note of at most 1000 characters.");
        command = command with { Note = command.Note?.Trim() };
        var result = await compositions.ReviewAsync(ownerId, draftId, command, clock.GetUtcNow().UtcDateTime, ct);
        return result is null ? null : CompositionDto.From(result);
    }
    public Task<IReadOnlyList<CompositionSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct)
    { if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new InvalidResumeException("Invalid composition pagination."); return compositions.GetPageAsync(ownerId, page, size, ct); }
}
