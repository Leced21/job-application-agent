using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using JobApplicationAgent.Collection.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class SourcesControllerTests : IClassFixture<CollectionApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/collection/sources/local-json/collect";
    private readonly CollectionApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private readonly HttpClient client;
    public SourcesControllerTests(CollectionApiFactory factory)
    {
        this.factory = factory;
        // Each test starts a fresh limiter, while retaining the fixture's isolated database.
        host = factory.WithWebHostBuilder(_ => { });
        client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CollectionApiFactory.ServiceToken);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private static ImportOfferCommand Offer(string id) => new(" Data Engineer ", "Company", WorkMode: "Télétravail", ContractType: "CDI",
        Skills: ["pyspark", "SQL"], SourceName: "Unverified platform claim", SourceUrl: $"https://example.invalid/{id}");

    [Fact]
    public async Task AuthorizedExport_ShouldReuseNormalizationRevisionsAndDeduplication_WithoutPublishing()
    {
        var sources = await client.GetFromJsonAsync<SourceDescriptor[]>("/api/v1/collection/sources");
        Assert.Contains(sources!, source => source.Id == "local-json");
        Assert.Contains(sources!, source => source.Id == "adzuna");
        var request = new SourceCollectionRequest([Offer("a"), Offer("b"), Offer("a")]);
        using var response = await client.PostAsJsonAsync(Route, request);
        response.EnsureSuccessStatusCode();
        var report = (await response.Content.ReadFromJsonAsync<SourceCollectionResponse>())!;
        Assert.Equal(3, report.Received);
        Assert.Equal(2, report.Created);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(new[] { 0, 1, 2 }, report.Items.Select(x => x.Index));
        Assert.Equal(report.Items[0].OfferId, report.Items[2].OfferId);
        var page = (await client.GetFromJsonAsync<ImportedOfferPage>("/api/v1/collection/imports"))!;
        Assert.All(page.Items, item =>
        {
            Assert.Equal("LocalJsonExport", item.SourceName);
            Assert.Equal("Remote", item.WorkMode);
            Assert.Equal("PendingReview", item.Status);
        });
        var id = report.Items[0].OfferId;
        var revisions = (await client.GetFromJsonAsync<OfferRevisionDto[]>($"/api/v1/collection/imports/{id}/revisions"))!;
        Assert.Equal("Pending", Assert.Single(revisions).Decision);
        var changedResponse = await client.PostAsJsonAsync(Route, new SourceCollectionRequest([Offer("a") with { Title = "Senior Engineer" }]));
        Assert.Equal(1, (await changedResponse.Content.ReadFromJsonAsync<SourceCollectionResponse>())!.Changed);
        Assert.Equal(2, (await client.GetFromJsonAsync<OfferRevisionDto[]>($"/api/v1/collection/imports/{id}/revisions"))!.Length);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("null-item")]
    [InlineData("invalid-item")]
    public async Task InvalidBatch_ShouldRejectEverything(string kind)
    {
        var request = kind switch
        {
            "null" => new SourceCollectionRequest(null),
            "empty" => new SourceCollectionRequest([]),
            "oversize" => new SourceCollectionRequest(Enumerable.Repeat<ImportOfferCommand?>(Offer("a"), 101).ToArray()),
            "null-item" => new SourceCollectionRequest([Offer("a"), null]),
            _ => new SourceCollectionRequest([Offer("a"), Offer("b") with { SalaryMin = -1 }])
        };
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<ImportedOfferPage>("/api/v1/collection/imports"))!.TotalCount);
    }

    [Fact]
    public async Task UnknownSource_ShouldNotFallBackToManualOrNetwork()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/collection/sources/unknown/collect",
            new SourceCollectionRequest([Offer("a")]))).StatusCode);
    }

    [Fact]
    public async Task AdzunaWithoutCredentials_ShouldReturnUnavailableWithoutCallingProvider()
    {
        using var response = await client.PostAsJsonAsync("/api/v1/collection/sources/adzuna/collect",
            new SourceCollectionRequest(null, What: ".NET developer", Where: "Lyon"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("app_key", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quota_ShouldRejectEleventhRequestWithoutImportingIt()
    {
        for (var index = 0; index < 10; index++)
            (await client.PostAsJsonAsync(Route, new SourceCollectionRequest([Offer(index.ToString())]))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(Route,
            new SourceCollectionRequest([Offer("rejected")]))).StatusCode);
        Assert.Equal(10, (await client.GetFromJsonAsync<ImportedOfferPage>("/api/v1/collection/imports"))!.TotalCount);
    }

    [Fact]
    public async Task ConcurrentReversedBatches_ShouldAvoidDeadlocksAndKeepOneIdentityPerOffer()
    {
        var offers = new[] { Offer("a"), Offer("b"), Offer("c") };
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Route, new SourceCollectionRequest(offers)),
            client.PostAsJsonAsync(Route, new SourceCollectionRequest(offers.Reverse().ToArray())));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses) response.Dispose();
        var page = (await client.GetFromJsonAsync<ImportedOfferPage>("/api/v1/collection/imports"))!;
        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, offer => Assert.Equal(2, offer.SeenCount));
    }

    private sealed class FailSecondSave : SaveChangesInterceptor
    {
        private int count;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref count) == 2) throw new InvalidOperationException("Simulated second-write failure.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task StorageFailureAfterFirstWrite_ShouldRollbackWholeBatch()
    {
        var failure = new FailSecondSave();
        using var failingHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CollectionDbContext>(options => options.AddInterceptors(failure))));
        using var failingClient = failingHost.CreateClient();
        failingClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CollectionApiFactory.ServiceToken);
        Assert.Equal(HttpStatusCode.InternalServerError, (await failingClient.PostAsJsonAsync(Route,
            new SourceCollectionRequest([Offer("a"), Offer("b")]))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        Assert.Equal(0, await db.Offers.CountAsync());
        Assert.Equal(0, await db.Revisions.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
    }
}
