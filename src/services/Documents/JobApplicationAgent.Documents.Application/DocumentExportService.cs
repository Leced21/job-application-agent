using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record CreateDocumentExportCommand(string Format, string ContentSha256);
public sealed record DocumentExportMetadata(Guid Id, Guid DraftId, string Format, string RendererVersion, string CompositionSha256,
    string FileSha256, string FileName, string MediaType, int Length, int? PageCount, DateTime CreatedAtUtc, DateTime ApprovedAtUtc)
{
    public static DocumentExportMetadata From(DocumentExport value) => new(value.Id, value.DraftId, value.Format, value.RendererVersion,
        value.CompositionSha256, value.FileSha256, value.FileName, value.MediaType, value.Length, value.PageCount, value.CreatedAtUtc, value.ApprovedAtUtc);
}
public sealed record DocumentExportWriteResult(DocumentExportMetadata Export, bool Created);
public sealed record RenderedDocument(byte[] Content, int? PageCount);
public sealed record ExportPreview(DocumentExport File, string? PlainText);
public sealed class DocumentExportUnavailableException : Exception;
public interface IApprovedDocumentRenderer
{
    string Version { get; }
    Task<RenderedDocument> RenderAsync(ComposedDocumentContent content, string format, CancellationToken ct);
}
public interface IDocumentExportRepository
{
    Task<DocumentExport?> GetAsync(Guid ownerId, Guid id, CancellationToken ct);
    Task<DocumentExportMetadata?> GetMetadataAsync(Guid ownerId, Guid id, CancellationToken ct);
    Task<DocumentExportMetadata?> FindAsync(Guid ownerId, Guid draftId, string format, CancellationToken ct);
    Task<(DocumentExportMetadata Export, bool Created)> SaveOrGetAsync(DocumentExport export, CancellationToken ct);
    Task<IReadOnlyList<DocumentExportMetadata>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct);
}
public sealed class DocumentExportService(IDocumentCompositionRepository compositions, IDocumentExportRepository exports,
    IApprovedDocumentRenderer renderer, TimeProvider clock)
{
    public async Task<DocumentExportWriteResult?> CreateAsync(Guid ownerId, Guid draftId, CreateDocumentExportCommand command, CancellationToken ct)
    {
        if (command.Format is not ("Pdf" or "Docx") || command.ContentSha256 is null ||
            !System.Text.RegularExpressions.Regex.IsMatch(command.ContentSha256, "^[a-f0-9]{64}$"))
            throw new InvalidResumeException("Choose Pdf or Docx and the exact approved composition SHA-256.");
        var composition = await compositions.GetAsync(ownerId, draftId, ct);
        if (composition is null) return null;
        ValidateApproval(composition, ownerId, command.ContentSha256);
        var existing = await exports.FindAsync(ownerId, draftId, command.Format, ct);
        if (existing is not null)
        {
            if (existing.CompositionSha256 != command.ContentSha256) throw new DraftConflictException("This format already belongs to another content hash.");
            return new(existing, false);
        }
        var content = JsonSerializer.Deserialize<ComposedDocumentContent>(composition.ContentJson)!;
        var rendered = await renderer.RenderAsync(content, command.Format, ct);
        if (rendered.Content.Length > 5242880) throw new DraftRejectedException("The rendered file exceeds 5 MiB.");
        var export = DocumentExport.Create(ownerId, draftId, command.Format, renderer.Version, command.ContentSha256, rendered.Content,
            rendered.PageCount, composition.ReviewedAtUtc!.Value, clock.GetUtcNow().UtcDateTime);
        var saved = await exports.SaveOrGetAsync(export, ct);
        return new(saved.Export, saved.Created);
    }
    public Task<DocumentExportMetadata?> GetMetadataAsync(Guid ownerId, Guid id, CancellationToken ct) => exports.GetMetadataAsync(ownerId, id, ct);
    public async Task<ExportPreview?> GetFileAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        var file = await exports.GetAsync(ownerId, id, ct);
        if (file is null) return null;
        var composition = await compositions.GetAsync(ownerId, file.DraftId, ct);
        if (composition is null) return null;
        ValidateApproval(composition, ownerId, file.CompositionSha256);
        if (Convert.ToHexStringLower(SHA256.HashData(file.Content)) != file.FileSha256)
            throw new DraftRejectedException("The stored file does not match its hash.");
        return new(file, file.Format == "Docx" ? JsonSerializer.Deserialize<ComposedDocumentContent>(composition.ContentJson)!.PlainText : null);
    }
    public Task<IReadOnlyList<DocumentExportMetadata>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct)
    { if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new InvalidResumeException("Invalid export pagination."); return exports.GetPageAsync(ownerId, page, size, ct); }
    private static void ValidateApproval(DocumentComposition composition, Guid ownerId, string hash)
    {
        if (composition.Decision != "Approved" || composition.ReviewedByOwnerId != ownerId || composition.ReviewedAtUtc is null)
            throw new DraftRejectedException("Approve the exact composed content before exporting or previewing its file.");
        if (composition.ContentSha256 != hash) throw new DraftConflictException("The requested hash differs from the approved content.");
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(composition.ContentJson))) != hash)
            throw new DraftRejectedException("The composition does not match its approved hash.");
    }
}
