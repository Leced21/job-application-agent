using FluentValidation;
using FluentValidation.Results;
using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Connectors;

public sealed record SourceDescriptor(string Id, string Name, string Transport, int MaxOffersPerBatch, bool AllowsEmptyResult = false);
public sealed record SourceCollectionRequest(
    ImportOfferCommand?[]? Offers,
    string? What = null,
    string? Where = null,
    int Page = 1);
public sealed record SourceCollectionItem(int Index, Guid OfferId, bool IsDuplicate, bool ContentChanged);
public sealed record SourceCollectionResponse(string SourceId, int Received, int Created, int Duplicates,
    int Changed, IReadOnlyList<SourceCollectionItem> Items);
public sealed class CollectionSourceNotFoundException : Exception;
public sealed class CollectionSourceTimeoutException : Exception;
public sealed class CollectionSourceUnavailableException(string message) : Exception(message);

public interface IOfferSourceConnector
{
    SourceDescriptor Descriptor { get; }
    Task<IReadOnlyList<ImportOfferCommand>> ReadAsync(SourceCollectionRequest request, CancellationToken ct);
}

public interface ICollectedOfferBatchRepository
{
    Task<IReadOnlyList<ImportPersistenceResult>> ImportBatchAsync(IReadOnlyList<CollectedOffer> offers, CancellationToken ct);
}

public sealed class CollectSourceHandler(IEnumerable<IOfferSourceConnector> connectors,
    IValidator<ImportOfferCommand> validator, OfferNormalizer normalizer,
    ICollectedOfferBatchRepository repository, TimeProvider clock)
{
    public IReadOnlyList<SourceDescriptor> GetSources() => connectors.Select(x => x.Descriptor).OrderBy(x => x.Id).ToArray();

    public async Task<SourceCollectionResponse> HandleAsync(string sourceId, SourceCollectionRequest request, CancellationToken ct)
    {
        var connector = connectors.SingleOrDefault(x => x.Descriptor.Id == sourceId)
            ?? throw new CollectionSourceNotFoundException();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var commands = await connector.ReadAsync(request, deadline.Token);
            if ((commands.Count == 0 && !connector.Descriptor.AllowsEmptyResult) ||
                commands.Count > connector.Descriptor.MaxOffersPerBatch || commands.Count > 100)
                throw new ValidationException("A collection batch must contain 1–100 offers within source limits.");
            if (commands.Count == 0) return new(sourceId, 0, 0, 0, 0, []);
            var failures = new List<ValidationFailure>();
            var offers = new List<CollectedOffer>(commands.Count);
            var now = clock.GetUtcNow().UtcDateTime;
            for (var index = 0; index < commands.Count; index++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var validation = await validator.ValidateAsync(commands[index], deadline.Token);
                failures.AddRange(validation.Errors.Select(error => new ValidationFailure($"Offers[{index}].{error.PropertyName}", error.ErrorMessage)));
                if (validation.IsValid) offers.Add(normalizer.Normalize(commands[index], now));
            }
            if (failures.Count != 0) throw new ValidationException(failures);
            var results = await repository.ImportBatchAsync(offers, deadline.Token);
            return new(sourceId, commands.Count, results.Count(x => !x.IsDuplicate), results.Count(x => x.IsDuplicate),
                results.Count(x => x.ContentChanged), results.Select((x, index) =>
                    new SourceCollectionItem(index, x.Offer.Id, x.IsDuplicate, x.ContentChanged)).ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CollectionSourceTimeoutException();
        }
    }
}
