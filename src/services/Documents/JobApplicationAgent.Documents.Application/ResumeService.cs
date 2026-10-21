using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed class InvalidResumeException(string message) : Exception(message);
public sealed record ResumeMetadata(Guid Id, string FileName, string MediaType, int Length, string Sha256, DateTime UploadedAtUtc, string Status)
{
    public static ResumeMetadata From(ResumeDocument document) => new(document.Id, document.FileName, document.MediaType,
        document.Length, document.Sha256, document.UploadedAtUtc, document.Status);
}
public interface IResumeRepository
{
    Task AddAsync(ResumeDocument document, CancellationToken ct);
    Task<ResumeDocument?> GetAsync(Guid ownerId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ResumeMetadata>> GetPageAsync(Guid ownerId, int page, int pageSize, CancellationToken ct);
}

public sealed class ResumeService(IResumeRepository repository, TimeProvider clock)
{
    public async Task<ResumeMetadata> UploadAsync(Guid ownerId, string fileName, string mediaType, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await content.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > ResumeFileValidator.MaxBytes) throw new InvalidResumeException("A resume must be at most 5 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        var validated = ResumeFileValidator.Validate(fileName, mediaType, bytes);
        var document = ResumeDocument.Create(ownerId, validated.FileName, validated.MediaType, bytes, clock.GetUtcNow().UtcDateTime);
        await repository.AddAsync(document, ct);
        return ResumeMetadata.From(document);
    }
    public Task<ResumeDocument?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => repository.GetAsync(ownerId, id, ct);
    public Task<IReadOnlyList<ResumeMetadata>> GetPageAsync(Guid ownerId, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100) throw new InvalidResumeException("Invalid pagination.");
        return repository.GetPageAsync(ownerId, page, pageSize, ct);
    }
}
