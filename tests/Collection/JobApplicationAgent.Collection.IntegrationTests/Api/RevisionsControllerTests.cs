using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.IntegrationTests.Infrastructure;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class RevisionsControllerTests(CollectionApiFactory factory) : IClassFixture<CollectionApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/collection/imports";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static ImportOfferCommand Command() => new("Engineer", "Company", Skills: ["SQL"], SourceUrl: "https://example.invalid/1");
    private static async Task<Guid> Import(HttpClient client, ImportOfferCommand command)
    {
        using var response = await client.PostAsJsonAsync(Route, command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportOfferResponse>())!.Offer.Id;
    }

    [Fact]
    public async Task Migration_ShouldBackfillExistingImports_WithUtcDatesAndOriginalContent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CollectionDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261015112946_InitialCollectionSchema");
        try
        {
            var offer = new OfferNormalizer().Normalize(Command() with
            {
                PublishedAtUtc = factory.Clock.Now.UtcDateTime,
                ExpiresAtUtc = factory.Clock.Now.AddDays(1).UtcDateTime,
                SalaryMin = 45000, SalaryCurrency = "EUR"
            }, factory.Clock.Now.UtcDateTime);
            db.Offers.Add(offer);
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            var revision = await db.Revisions.SingleAsync();
            Assert.Equal(offer.Id, revision.OfferId);
            Assert.Equal(offer.ContentFingerprint, revision.Fingerprint);
            Assert.Equal("Pending", revision.Decision);
            Assert.Null(revision.ReviewedByOwnerId);
            Assert.Equal(offer.SalaryMin, revision.ReadContent().SalaryMin);
            Assert.Equal(offer.PublishedAtUtc, revision.ReadContent().PublishedAtUtc);
            Assert.Equal(DateTimeKind.Utc, revision.ReadContent().ExpiresAtUtc!.Value.Kind);
            Assert.Equal(offer.ExpiresAtUtc, revision.ReadContent().ExpiresAtUtc);
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally
        {
            await migrator.MigrateAsync();
        }
    }

    [Fact]
    public async Task ChangedReimports_ShouldPreserveSnapshots_AndDeduplicateConcurrentRevisions()
    {
        using var client = factory.CreateClient();
        var id = await Import(client, Command());
        factory.Clock.Now = factory.Clock.Now.AddHours(1);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Import(client, Command() with { Title = "Senior Engineer", Skills = ["C#"] })));
        var revisions = (await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!;
        Assert.Equal(2, revisions.Length);
        Assert.Equal("Senior Engineer", revisions[0].Content.Title);
        Assert.Equal(new[] { "C#" }, revisions[0].Content.Skills);
        Assert.Equal("Engineer", revisions[1].Content.Title);
        Assert.All(revisions, revision => Assert.Equal("Pending", revision.Decision));
        Assert.Equal(9, (await client.GetFromJsonAsync<ImportedOfferDto>($"{Route}/{id}"))!.SeenCount);
        Assert.Single((await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions?pageSize=1&page=2"))!);
    }

    [Fact]
    public async Task Review_ShouldBeFinalIdempotent_AndLeaveOriginalImportUnchanged()
    {
        using var client = factory.CreateClient();
        var id = await Import(client, Command());
        await Import(client, Command() with { Title = "Changed" });
        var revision = (await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!.Single(x => x.Content.Title == "Changed");
        var path = $"{Route}/{id}/revisions/{revision.Id}/review";
        using var approved = await client.PostAsJsonAsync(path, new ReviewOfferCommand("Approved"));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var reviewed = (await approved.Content.ReadFromJsonAsync<OfferRevisionDto>())!;
        Assert.Equal(factory.Clock.Now.UtcDateTime, reviewed.ReviewedAtUtc);
        Assert.Equal(CollectionApiFactory.TestOwnerId, reviewed.ReviewedByOwnerId);
        factory.Clock.Now = factory.Clock.Now.AddHours(1);
        using var repeated = await client.PostAsJsonAsync(path, new ReviewOfferCommand("Approved"));
        var repeatedRevision = (await repeated.Content.ReadFromJsonAsync<OfferRevisionDto>())!;
        Assert.Equal(reviewed.ReviewedAtUtc, repeatedRevision.ReviewedAtUtc);
        Assert.Equal(reviewed.ReviewedByOwnerId, repeatedRevision.ReviewedByOwnerId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new ReviewOfferCommand("Rejected"))).StatusCode);
        Assert.Equal("Engineer", (await client.GetFromJsonAsync<ImportedOfferDto>($"{Route}/{id}"))!.Title);
    }

    [Fact]
    public async Task ConflictingConcurrentReviews_ShouldCommitOnlyOneDecision()
    {
        using var client = factory.CreateClient();
        var id = await Import(client, Command());
        var revision = Assert.Single((await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!);
        var path = $"{Route}/{id}/revisions/{revision.Id}/review";
        var responses = await Task.WhenAll(client.PostAsJsonAsync(path, new ReviewOfferCommand("Approved")),
            client.PostAsJsonAsync(path, new ReviewOfferCommand("Rejected")));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        var reviewed = (await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!.Single();
        Assert.Equal(CollectionApiFactory.TestOwnerId, reviewed.ReviewedByOwnerId);
    }

    [Fact]
    public async Task Review_ShouldValidateExpiryOwnershipAndInput()
    {
        using var client = factory.CreateClient();
        var id = await Import(client, Command() with { ExpiresAtUtc = factory.Clock.Now.UtcDateTime });
        var revision = Assert.Single((await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!);
        var path = $"{Route}/{id}/revisions/{revision.Id}/review";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new ReviewOfferCommand("Approved"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new ReviewOfferCommand("invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Route}/{Guid.NewGuid()}/revisions/{revision.Id}/review", new ReviewOfferCommand("Rejected"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{Guid.NewGuid()}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Route}/{id}/revisions?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, new ReviewOfferCommand("Rejected"))).StatusCode);
    }
}
