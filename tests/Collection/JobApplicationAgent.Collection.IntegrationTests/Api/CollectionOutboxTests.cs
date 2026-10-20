using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Collection.Application.Events;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using JobApplicationAgent.Collection.Infrastructure.Events;
using JobApplicationAgent.Collection.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class CollectionOutboxTests(CollectionApiFactory factory) : IClassFixture<CollectionApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/collection/imports";
    private static ImportOfferCommand Command() => new("Engineer", "Company", SourceUrl: "https://example.invalid/outbox");
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();

    [Fact]
    public async Task ConcurrentImportsAndRevisions_ShouldRecordOneEventPerDistinctRevision()
    {
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route, Command())));
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var initial = Assert.Single(await db.OutboxMessages.AsNoTracking().ToArrayAsync());
        Assert.Equal(8, (await db.Offers.SingleAsync()).SeenCount);
        Assert.Equal((await db.Revisions.SingleAsync()).Id, initial.RevisionId);
        using var payload = JsonDocument.Parse(initial.PayloadJson);
        Assert.Equal(initial.Id, payload.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(initial.OccurredAtUtc, payload.RootElement.GetProperty("occurredAtUtc").GetDateTime());
        var changed = Command() with { Title = "Senior Engineer" };
        var changedResponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route, changed)));
        foreach (var response in changedResponses) { response.EnsureSuccessStatusCode(); response.Dispose(); }
        Assert.Equal(2, await db.Revisions.CountAsync());
        var messages = await db.OutboxMessages.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, messages.Length);
        Assert.Contains(messages, x => x.Id == initial.Id && x.PayloadJson == initial.PayloadJson);
        Assert.All(messages, x => { Assert.Null(x.DispatchedAtUtc); Assert.Equal(OfferRevisionRecordedV1.Type, x.EventType); });
    }

    [Fact]
    public async Task OutboxWriteFailure_ShouldRollbackOfferAndRevision()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CollectionDbContext>(options => options.AddInterceptors(new CorruptOutboxPayload()))));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CollectionApiFactory.ServiceToken);
        using var response = await client.PostAsJsonAsync(Route, Command());
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        Assert.Equal(0, await db.Offers.CountAsync());
        Assert.Equal(0, await db.Revisions.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Dispatcher_ShouldMarkOnlyAfterConfirmedPublish_AndKeepStableEventId()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Command()); response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var publisher = new RecordingPublisher();
        var dispatcher = new CollectionOutboxDispatchService(db, publisher, factory.Clock);

        Assert.True((await dispatcher.DispatchOneAsync(CancellationToken.None))!.Published);
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(message.Id, Assert.Single(publisher.EventIds));
        Assert.NotNull(message.DispatchedAtUtc);
        Assert.Equal(0, message.AttemptCount);
        Assert.Null(await dispatcher.DispatchOneAsync(CancellationToken.None));
        Assert.Single(publisher.EventIds);
    }

    [Fact]
    public async Task DispatcherFailure_ShouldScheduleDurableRetryWithoutLosingEvent()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Command()); response.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var publisher = new RecordingPublisher { Fail = true };
        var dispatcher = new CollectionOutboxDispatchService(db, publisher, factory.Clock);

        Assert.False((await dispatcher.DispatchOneAsync(CancellationToken.None))!.Published);
        var failed = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Null(failed.DispatchedAtUtc);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Equal(nameof(IOException), failed.LastFailureCode);
        Assert.NotNull(failed.NextAttemptAtUtc);
        Assert.Null(await dispatcher.DispatchOneAsync(CancellationToken.None));
        Assert.Empty(publisher.EventIds);

        factory.Clock.Now = new(failed.NextAttemptAtUtc!.Value.AddSeconds(1));
        publisher.Fail = false;
        Assert.True((await dispatcher.DispatchOneAsync(CancellationToken.None))!.Published);
        var delivered = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.NotNull(delivered.DispatchedAtUtc);
        Assert.Equal(1, delivered.AttemptCount);
        Assert.Null(delivered.LastFailureCode);
        Assert.Single(publisher.EventIds);
        Assert.Equal(delivered.Id, publisher.EventIds[0]);
    }

    private sealed class RecordingPublisher : JobApplicationAgent.Collection.Application.Events.ICollectionEventPublisher
    {
        public List<Guid> EventIds { get; } = [];
        public bool Fail { get; set; }
        public Task PublishAsync(CollectionOutboxMessage message, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("Broker is unavailable.");
            EventIds.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class CorruptOutboxPayload : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<CollectionOutboxMessage>().Where(x => x.State == EntityState.Added))
                entry.Property(x => x.PayloadJson).CurrentValue = "invalid-json";
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Migration_ShouldBackfillExistingRevisionsWithoutChangingThem()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015114728_AddOfferRevisions");
        try
        {
            var offer = new OfferNormalizer().Normalize(Command(), factory.Clock.Now.UtcDateTime);
            var revision = OfferRevision.From(offer, offer.Id);
            revision.Review("Approved", factory.Clock.Now.UtcDateTime, CollectionApiFactory.TestOwnerId);
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO offer_revisions ("Id", "OfferId", "Fingerprint", "OriginalTitle", "ContentJson",
                    "NormalizationVersion", "CollectedAtUtc", "Decision", "ReviewedAtUtc")
                VALUES ({revision.Id}, {revision.OfferId}, {revision.Fingerprint}, {revision.OriginalTitle},
                    CAST({revision.ContentJson} AS jsonb), {revision.NormalizationVersion}, {revision.CollectedAtUtc},
                    {revision.Decision}, {revision.ReviewedAtUtc})
                """);
            await migrator.MigrateAsync();
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
            using var payload = JsonDocument.Parse(message.PayloadJson);
            Assert.Equal(message.Id, payload.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal(revision.Id, message.RevisionId);
            Assert.Equal(offer.Id, payload.RootElement.GetProperty("offerId").GetGuid());
            Assert.Equal(message.OccurredAtUtc, payload.RootElement.GetProperty("occurredAtUtc").GetDateTime());
            Assert.Equal(revision.Fingerprint, payload.RootElement.GetProperty("fingerprint").GetString());
            Assert.Equal("Manual", payload.RootElement.GetProperty("sourceName").GetString());
            var storedRevision = await db.Revisions.AsNoTracking().SingleAsync();
            Assert.Equal("Approved", storedRevision.Decision);
            Assert.Equal(revision.ReviewedAtUtc, storedRevision.ReviewedAtUtc);
            Assert.Null(storedRevision.ReviewedByOwnerId);
            Assert.Null(message.DispatchedAtUtc);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { await migrator.MigrateAsync(); }
    }
}
