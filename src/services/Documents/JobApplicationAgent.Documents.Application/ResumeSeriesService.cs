using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record CreateResumeSeriesCommand(string Name, Guid FirstResumeId, string? Label = null);
public sealed record AddResumeVersionCommand(Guid ResumeId, string? Label = null);
public sealed record ResumeSeriesSummary(Guid Id, string Name, DateTime CreatedAtUtc, int VersionCount);
public sealed record ResumeSeriesVersionMetadata(Guid SeriesId, int VersionNumber, string? Label, DateTime AddedAtUtc, ResumeMetadata Resume);
public sealed record ResumeSeriesWriteResult(ResumeSeriesSummary Series, ResumeSeriesVersionMetadata Version, bool Created);
public interface IResumeSeriesRepository
{
    Task<ResumeSeriesWriteResult?> CreateAsync(Guid ownerId, string name, Guid resumeId, string? label, DateTime now, CancellationToken ct);
    Task<ResumeSeriesWriteResult?> AddVersionAsync(Guid ownerId, Guid seriesId, Guid resumeId, string? label, DateTime now, CancellationToken ct);
    Task<ResumeSeriesSummary?> GetAsync(Guid ownerId, Guid seriesId, CancellationToken ct);
    Task<ResumeSeriesSummary?> GetForResumeAsync(Guid ownerId, Guid resumeId, CancellationToken ct);
    Task<IReadOnlyList<ResumeSeriesSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct);
    Task<IReadOnlyList<ResumeSeriesVersionMetadata>?> GetVersionsAsync(Guid ownerId, Guid seriesId, int page, int size, CancellationToken ct);
    Task<ResumeSeriesVersionMetadata?> GetVersionAsync(Guid ownerId, Guid seriesId, int number, CancellationToken ct);
}
public sealed class ResumeSeriesService(IResumeSeriesRepository repository, TimeProvider clock)
{
    public Task<ResumeSeriesWriteResult?> CreateAsync(Guid ownerId, CreateResumeSeriesCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 200 || command.Name.Any(char.IsControl))
            throw new InvalidResumeException("A series name must contain 1–200 characters without control characters.");
        return repository.CreateAsync(ownerId, command.Name.Trim(), ValidId(command.FirstResumeId), Label(command.Label), clock.GetUtcNow().UtcDateTime, ct);
    }
    public Task<ResumeSeriesWriteResult?> AddVersionAsync(Guid ownerId, Guid seriesId, AddResumeVersionCommand command, CancellationToken ct) =>
        repository.AddVersionAsync(ownerId, seriesId, ValidId(command.ResumeId), Label(command.Label), clock.GetUtcNow().UtcDateTime, ct);
    public Task<ResumeSeriesSummary?> GetAsync(Guid ownerId, Guid seriesId, CancellationToken ct) => repository.GetAsync(ownerId, seriesId, ct);
    public Task<ResumeSeriesSummary?> GetForResumeAsync(Guid ownerId, Guid resumeId, CancellationToken ct) => repository.GetForResumeAsync(ownerId, resumeId, ct);
    public Task<IReadOnlyList<ResumeSeriesSummary>> GetPageAsync(Guid ownerId, int page, int size, CancellationToken ct)
    { ValidatePage(page, size); return repository.GetPageAsync(ownerId, page, size, ct); }
    public Task<IReadOnlyList<ResumeSeriesVersionMetadata>?> GetVersionsAsync(Guid ownerId, Guid seriesId, int page, int size, CancellationToken ct)
    { ValidatePage(page, size); return repository.GetVersionsAsync(ownerId, seriesId, page, size, ct); }
    public Task<ResumeSeriesVersionMetadata?> GetVersionAsync(Guid ownerId, Guid seriesId, int number, CancellationToken ct)
    {
        if (number is < 1 or > 1000) throw new InvalidResumeException("Invalid version number.");
        return repository.GetVersionAsync(ownerId, seriesId, number, ct);
    }
    private static Guid ValidId(Guid id) => id == Guid.Empty ? throw new InvalidResumeException("A resume identifier is required.") : id;
    private static string? Label(string? value)
    {
        if (value?.Trim().Length > 200 || value?.Any(char.IsControl) == true)
            throw new InvalidResumeException("A version label must contain at most 200 characters without controls.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    private static void ValidatePage(int page, int size)
    { if (page is < 1 or > 1000000 || size is < 1 or > 100) throw new InvalidResumeException("Invalid pagination."); }
}
