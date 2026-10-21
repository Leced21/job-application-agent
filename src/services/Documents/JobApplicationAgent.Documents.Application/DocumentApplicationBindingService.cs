using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record DocumentApplicationScope(bool Enabled, Guid OwnerId, Guid ProfileId);
public sealed record ConfirmDocumentApplicationBinding(Guid ProfileId, string FileSha256, bool Confirmed);
public interface IDocumentApplicationBindingRepository
{
    Task<DocumentApplicationBinding?> GetAsync(Guid ownerId, Guid exportId, CancellationToken ct);
    Task<DocumentApplicationBinding> SaveOrGetAsync(DocumentApplicationBinding binding, CancellationToken ct);
}
public sealed class DocumentApplicationBindingService(DocumentExportService exports, IPreparedDraftRepository drafts,
    IDocumentCompositionRepository compositions, IDocumentApplicationBindingRepository repository, DocumentApplicationScope scope, TimeProvider clock)
{
    public async Task<DocumentApplicationBinding?> ConfirmAsync(Guid owner, Guid exportId, ConfirmDocumentApplicationBinding command, CancellationToken ct)
    {
        if (!scope.Enabled || owner != scope.OwnerId || command.ProfileId != scope.ProfileId || !command.Confirmed)
            throw new DraftRejectedException("Explicitly confirm the configured owner's profile.");
        var source = await SourceAsync(owner, exportId, ct);
        if (source is null) return null;
        if (command.FileSha256 != source.Value.File.FileSha256)
            throw new DraftConflictException("Confirm the exact exported file hash.");
        return await repository.SaveOrGetAsync(DocumentApplicationBinding.Create(source.Value.File, source.Value.Draft,
            scope.ProfileId, clock.GetUtcNow().UtcDateTime), ct);
    }
    public async Task<DocumentApplicationBinding?> GetVerifiedAsync(Guid owner, Guid exportId, CancellationToken ct)
    {
        if (!scope.Enabled || owner != scope.OwnerId) return null;
        var binding = await repository.GetAsync(owner, exportId, ct);
        if (binding is null || binding.ProfileId != scope.ProfileId) return null;
        var source = await SourceAsync(owner, exportId, ct);
        if (source is null) return null;
        var (file, draft) = source.Value;
        if (binding.DraftId != draft.Id || binding.JobId != draft.JobId || binding.Kind != draft.Kind ||
            binding.FileSha256 != file.FileSha256 || binding.CompositionSha256 != file.CompositionSha256 || binding.SnapshotSha256 != draft.SnapshotSha256)
            throw new DraftRejectedException("The confirmed binding no longer matches its sources.");
        return binding;
    }
    private async Task<(DocumentExport File, PreparedDocumentDraft Draft)?> SourceAsync(Guid owner, Guid exportId, CancellationToken ct)
    {
        var file = await exports.GetFileAsync(owner, exportId, ct); // Rechecks approval, composition and exact file bytes.
        if (file is null) return null;
        var draft = await drafts.GetAsync(owner, file.File.DraftId, ct);
        var composition = await compositions.GetAsync(owner, file.File.DraftId, ct);
        if (draft is null || composition is null) return null;
        if (composition.Kind != draft.Kind || composition.SourceSnapshotSha256 != draft.SnapshotSha256)
            throw new DraftRejectedException("Composition and source snapshot disagree.");
        _ = VerifiedDocumentComposer.Compose(draft); // Validates frozen facts and snapshot hash; no renderer or external call.
        return (file.File, draft);
    }
}
