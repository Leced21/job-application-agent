using FluentValidation;
using JobApplicationAgent.Collection.Application.Abstractions;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed class ImportOfferHandler(IValidator<ImportOfferCommand> validator, OfferNormalizer normalizer,
    ICollectedOfferRepository repository, TimeProvider clock)
{
    public async Task<ImportPersistenceResult> HandleAsync(ImportOfferCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var offer = normalizer.Normalize(command, clock.GetUtcNow().UtcDateTime);
        return await repository.ImportAsync(offer, cancellationToken);
    }
}
