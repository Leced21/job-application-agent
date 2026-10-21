using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using JobApplicationAgent.Collection.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class ImportsControllerTests(CollectionApiFactory factory) : IClassFixture<CollectionApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/collection/imports";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static ImportOfferCommand Command() => new(" Data  Engineer ", "Example Company", "Paris", "Télétravail", "CDI",
        50000m, 65000m, "eur", "Python and Spark", ["pyspark", "SQL"], "Manual", "https://example.invalid/jobs/1?utm_source=a");

    [Fact]
    public async Task Imports_ShouldRejectRequestsWithoutBearer()
    {
        using var client = factory.CreateUnauthenticatedClient();

        using var response = await client.PostAsJsonAsync(Route, Command());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdmin_CanReadButCannotMutateOrReviewOrPublish()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Collection:Admin:OwnerIds"] = Guid.NewGuid().ToString("D")
            })));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", CollectionApiFactory.ServiceToken);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/collection/sources")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Route, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/collection/sources/local-json/collect",
            new { offers = new[] { Command() } })).StatusCode);
        var offerId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"{Route}/{offerId}/revisions/{revisionId}/review", new { decision = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(
            $"{Route}/{offerId}/revisions/{revisionId}/publish", null)).StatusCode);
    }

    [Fact]
    public async Task Import_ShouldPersistNormalizedOffer_AndExposeProvenanceWithoutFetchingUrl()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Command());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var imported = await response.Content.ReadFromJsonAsync<ImportOfferResponse>();
        Assert.False(imported!.IsDuplicate);
        Assert.False(imported.ContentChanged);
        var saved = await client.GetFromJsonAsync<ImportedOfferDto>(response.Headers.Location);
        Assert.NotNull(saved);
        Assert.Equal(imported.Offer.Id, saved.Id);
        Assert.Equal("Data Engineer", saved.Title);
        Assert.Equal(" Data  Engineer ", saved.OriginalTitle);
        Assert.Equal("Remote", saved.WorkMode);
        Assert.Equal("Permanent", saved.ContractType);
        Assert.Equal("EUR", saved.SalaryCurrency);
        Assert.Equal("https://example.invalid/jobs/1", saved.SourceUrl);
        Assert.Equal("Manual", saved.SourceName);
        Assert.Equal(new[] { "Apache Spark", "SQL" }, saved.Skills);
        Assert.Equal(factory.Clock.Now.UtcDateTime, saved.FirstCollectedAtUtc);
        Assert.Equal("PendingReview", saved.Status);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Import_ShouldDeduplicateCanonicalUrl_AndKeepFirstContentWhenChanged()
    {
        using var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync(Route, Command());
        var initial = (await first.Content.ReadFromJsonAsync<ImportOfferResponse>())!.Offer;
        factory.Clock.Now = factory.Clock.Now.AddHours(1);
        using var repeated = await client.PostAsJsonAsync(Route, Command() with
        {
            Title = "New title requires review", SourceUrl = "https://EXAMPLE.invalid:443/jobs/1#apply"
        });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var updated = (await repeated.Content.ReadFromJsonAsync<ImportOfferResponse>())!;
        Assert.True(updated.IsDuplicate);
        Assert.True(updated.ContentChanged);
        Assert.True(updated.Offer.HasPendingChanges);
        Assert.Equal(initial.Id, updated.Offer.Id);
        Assert.Equal(initial.Title, updated.Offer.Title);
        Assert.Equal(initial.FirstCollectedAtUtc, updated.Offer.FirstCollectedAtUtc);
        Assert.Equal(factory.Clock.Now.UtcDateTime, updated.Offer.LastSeenAtUtc);
        Assert.Equal(2, updated.Offer.SeenCount);
        Assert.Equal(1, (await client.GetFromJsonAsync<ImportedOfferPage>(Route))!.TotalCount);
    }

    [Fact]
    public async Task Import_ShouldDeduplicateContentWithoutUrl_ButNotDistinctSourceUrls()
    {
        using var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync(Route, Command() with { SourceUrl = null, SalaryMin = 50000m });
        var next = await client.PostAsJsonAsync(Route, Command() with { SourceUrl = null, SalaryMin = 50000.00m, Title = "data engineer" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.False((await next.Content.ReadFromJsonAsync<ImportOfferResponse>())!.ContentChanged);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Route, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Route,
            Command() with { SourceUrl = "https://example.invalid/jobs/2" })).StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<ImportedOfferPage>(Route))!.TotalCount);
    }

    [Fact]
    public async Task ConcurrentImports_ShouldCreateOneRow_AndCountEveryObservation()
    {
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route, Command())));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        foreach (var response in responses) response.Dispose();
        var page = await client.GetFromJsonAsync<ImportedOfferPage>(Route);
        Assert.Equal(8, Assert.Single(page!.Items).SeenCount);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Observation_ShouldNotMoveLastSeenBackwards()
    {
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(Route, Command());
        var initialTime = factory.Clock.Now.UtcDateTime;
        factory.Clock.Now = factory.Clock.Now.AddHours(-1);
        var response = await client.PostAsJsonAsync(Route, Command());
        var imported = (await response.Content.ReadFromJsonAsync<ImportOfferResponse>())!;
        Assert.Equal(initialTime, imported.Offer.LastSeenAtUtc);
        Assert.Equal(2, imported.Offer.SeenCount);
    }

    [Fact]
    public async Task Expiry_ShouldReflectExplicitDate_AsClockAdvances()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Route, Command() with { ExpiresAtUtc = factory.Clock.Now.AddHours(1).UtcDateTime });
        Assert.Equal("PendingReview", (await response.Content.ReadFromJsonAsync<ImportOfferResponse>())!.Offer.Status);
        factory.Clock.Now = factory.Clock.Now.AddHours(2);
        Assert.Equal("Expired", (await client.GetFromJsonAsync<ImportedOfferDto>(response.Headers.Location))!.Status);
    }

    [Fact]
    public async Task Pagination_ShouldExposeTotalsAndDistinctPages_AndEmptyResults()
    {
        using var client = factory.CreateClient();
        var empty = await client.GetFromJsonAsync<ImportedOfferPage>(Route);
        Assert.Equal(0, empty!.TotalCount);
        Assert.Equal(0, empty.TotalPages);
        Assert.Empty(empty.Items);
        for (var index = 0; index < 3; index++)
            (await client.PostAsJsonAsync(Route, Command() with { SourceUrl = $"https://example.invalid/job/{index}" })).EnsureSuccessStatusCode();
        var first = (await client.GetFromJsonAsync<ImportedOfferPage>(Route + "?page=1&pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<ImportedOfferPage>(Route + "?page=2&pageSize=2"))!;
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(offer => offer.Id).Distinct().Count());
        Assert.Empty((await client.GetFromJsonAsync<ImportedOfferPage>(Route + "?page=3&pageSize=2"))!.Items);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=1000001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=abc")]
    public async Task Pagination_ShouldRejectInvalidArguments(string query)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?" + query)).StatusCode);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("url")]
    [InlineData("salary")]
    [InlineData("mode")]
    [InlineData("skills")]
    public async Task Import_ShouldRejectInvalidRequest_WithoutPersisting(string field)
    {
        using var client = factory.CreateClient();
        var invalid = field switch
        {
            "title" => Command() with { Title = " " },
            "url" => Command() with { SourceUrl = "file:///private" },
            "salary" => Command() with { SalaryMin = -1 },
            "mode" => Command() with { WorkMode = "invalid" },
            _ => Command() with { Skills = [" "] }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, invalid)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<ImportedOfferPage>(Route))!.TotalCount);
    }

    [Fact]
    public async Task Read_ShouldReturn404ForMissingId_And400ForEmptyId()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route + "/" + Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "/" + Guid.Empty)).StatusCode);
    }
}
