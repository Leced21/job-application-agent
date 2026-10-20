using FluentValidation;
using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Application.Imports;

namespace JobApplicationAgent.Collection.Infrastructure.Connectors;

// The caller supplies an authorized export. No filesystem path or remote URL is opened.
public sealed class LocalJsonExportConnector : IOfferSourceConnector
{
    public SourceDescriptor Descriptor { get; } = new("local-json", "LocalJsonExport", "UploadedJson", 100);
    public Task<IReadOnlyList<ImportOfferCommand>> ReadAsync(SourceCollectionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Offers is null || request.Offers.Length is < 1 or > 100 || request.Offers.Any(x => x is null))
            throw new ValidationException("Offers must contain 1–100 non-null entries.");
        // Provenance identifies this connector rather than an unverified platform claimed by the uploaded data.
        return Task.FromResult<IReadOnlyList<ImportOfferCommand>>(request.Offers.Select(x => x! with
            { SourceName = Descriptor.Name }).ToArray());
    }
}
