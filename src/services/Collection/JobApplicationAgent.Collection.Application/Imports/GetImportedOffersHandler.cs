using FluentValidation;
using JobApplicationAgent.Collection.Application.Abstractions;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed class GetImportedOffersHandler(ICollectedOfferRepository repository, TimeProvider clock)
{
    public async Task<ImportedOfferDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty) throw new ValidationException("Import ID cannot be empty.");
        var offer = await repository.GetByIdAsync(id, cancellationToken);
        return offer is null ? null : ImportedOfferDto.From(offer, clock.GetUtcNow().UtcDateTime);
    }
    public async Task<ImportedOfferPage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            throw new ValidationException("Page must be 1–1000000 and pageSize 1–100.");
        var result = await repository.GetPageAsync(page, pageSize, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        return new ImportedOfferPage(result.Items.Select(offer => ImportedOfferDto.From(offer, now)).ToArray(),
            result.TotalCount, page, pageSize, result.TotalCount == 0 ? 0 : (result.TotalCount - 1) / pageSize + 1);
    }
}
