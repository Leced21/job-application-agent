using FluentValidation;
using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class CollectSourceHandlerTests
{
    private static readonly ImportOfferCommand Valid = new("Engineer", "Company");
    private static (CollectSourceHandler Handler, ICollectedOfferBatchRepository Repository) Handler(params ImportOfferCommand[] commands)
    {
        var connector = Substitute.For<IOfferSourceConnector>();
        connector.Descriptor.Returns(new SourceDescriptor("test", "Test", "Test", 100));
        connector.ReadAsync(Arg.Any<SourceCollectionRequest>(), Arg.Any<CancellationToken>()).Returns(commands);
        var repository = Substitute.For<ICollectedOfferBatchRepository>();
        return (new([connector], new ImportOfferCommandValidator(), new OfferNormalizer(), repository, TimeProvider.System), repository);
    }

    [Fact]
    public async Task UnknownSource_ShouldFailBeforeAnyPersistence()
    {
        var (handler, repository) = Handler(Valid);
        await Assert.ThrowsAsync<CollectionSourceNotFoundException>(() => handler.HandleAsync("missing", new([Valid]), default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task InvalidItem_ShouldReportItsIndex_AndRejectWholeBatch()
    {
        var (handler, repository) = Handler(Valid, Valid with { Title = " " });
        var error = await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync("test", new([Valid]), default));
        Assert.Contains(error.Errors, x => x.PropertyName == "Offers[1].Title");
        Assert.Empty(repository.ReceivedCalls());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SourceLimits_ShouldBeEnforcedBeforePersistence(int count)
    {
        var (handler, repository) = Handler(Enumerable.Repeat(Valid, count).ToArray());
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync("test", new([Valid]), default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task Cancellation_ShouldPropagateWithoutPersistence()
    {
        var (handler, repository) = Handler(Valid);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync("test", new([Valid]), cancellation.Token));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task ValidSource_ShouldPassNormalizedBatchWithServerDates()
    {
        var (handler, repository) = Handler(Valid with { WorkMode = "Télétravail", Skills = ["pyspark"] });
        repository.ImportBatchAsync(Arg.Any<IReadOnlyList<CollectedOffer>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<JobApplicationAgent.Collection.Application.Abstractions.ImportPersistenceResult>>(
                call.Arg<IReadOnlyList<CollectedOffer>>().Select(x =>
                    new JobApplicationAgent.Collection.Application.Abstractions.ImportPersistenceResult(x, false, false)).ToArray()));
        var result = await handler.HandleAsync("test", new([Valid]), default);
        Assert.Equal(1, result.Created);
        await repository.Received(1).ImportBatchAsync(Arg.Is<IReadOnlyList<CollectedOffer>>(items =>
            items.Count == 1 && items[0].WorkMode == "Remote" && items[0].Skills[0] == "Apache Spark" &&
            items[0].FirstCollectedAtUtc.Kind == DateTimeKind.Utc), Arg.Any<CancellationToken>());
    }
}
