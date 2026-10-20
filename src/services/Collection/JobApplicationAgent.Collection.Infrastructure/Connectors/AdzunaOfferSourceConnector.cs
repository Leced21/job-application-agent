using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Application.Imports;

namespace JobApplicationAgent.Collection.Infrastructure.Connectors;

public sealed class AdzunaOfferSourceConnector(IAdzunaJobsClient client) : IOfferSourceConnector
{
    public SourceDescriptor Descriptor { get; } = new("adzuna", "Adzuna", "REST API", 100, AllowsEmptyResult: true);

    public async Task<IReadOnlyList<ImportOfferCommand>> ReadAsync(SourceCollectionRequest request, CancellationToken ct)
    {
        if (request.Offers is { Length: > 0 })
            throw new FluentValidation.ValidationException("Adzuna searches use What, Where and Page; offers cannot be supplied.");
        if (string.IsNullOrWhiteSpace(request.What) || request.What.Length > 200 || (request.Where?.Length ?? 0) > 200 ||
            request.Page is < 1 or > 100)
            throw new FluentValidation.ValidationException("Adzuna searches require What and allow an optional Where and page from 1 to 100.");
        return await client.SearchAsync(request.What, request.Where, request.Page, ct);
    }
}
