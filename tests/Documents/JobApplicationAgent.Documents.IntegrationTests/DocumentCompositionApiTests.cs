using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class DocumentCompositionApiTests : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private readonly DocumentsApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private readonly HttpClient client;
    public DocumentCompositionApiTests(DocumentsApiFactory factory)
    {
        this.factory = factory; host = factory.WithWebHostBuilder(_ => { }); client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private static string Route(Guid id) => $"/api/v1/documents/drafts/{id}/composition";
    private async Task<CompositionSamples.Sample> Seed(string kind = "Resume", Guid? owner = null)
    {
        var sample = CompositionSamples.Create(kind, owner ?? factory.OwnerId, dates: true);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(sample.Resume); db.Extractions.Add(sample.Extraction); db.CandidateFacts.AddRange(sample.Facts); db.PreparedDrafts.Add(sample.Draft);
        await db.SaveChangesAsync(); return sample;
    }
    private async Task<CompositionDto> Compose(Guid id)
    {
        using var response = await client.PostAsync(Route(id), null); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompositionWriteResult>())!.Document;
    }
    [Theory]
    [InlineData("Resume")]
    [InlineData("CoverLetter")]
    public async Task Composition_ShouldPersistSourceBoundContentAndRequireSeparateReview(string kind)
    {
        var source = await Seed(kind); var id = source.Draft.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(id))).StatusCode);
        using var response = await client.PostAsync(Route(id), null); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CompositionWriteResult>())!.Document;
        Assert.Equal("Pending", created.Composition.Decision); Assert.Null(created.Composition.ReviewedAtUtc);
        Assert.Equal(source.Draft.SnapshotSha256, created.Composition.SourceSnapshotSha256);
        Assert.Equal(source.Facts.Select(x => x.Id), created.Content.Blocks.Select(x => x.FactId));
        Assert.Contains("No Azure certification claimed", created.Content.PlainText); Assert.DoesNotContain("Rust", created.Content.PlainText);
        Assert.Contains("Dates confirmées par le candidat : 2020-03-12", created.Content.PlainText);
        Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(await client.GetFromJsonAsync<CompositionDto>(response.Headers.Location)));
        Assert.Equal(JsonSerializer.Serialize(created), JsonSerializer.Serialize(await Compose(id)));
        using var review = await client.PostAsJsonAsync(Route(id) + "/review", new CompositionReviewCommand(created.Composition.ContentSha256, "Approved", true, "Checked all blocks and missing identity"));
        review.EnsureSuccessStatusCode(); var approved = (await review.Content.ReadFromJsonAsync<CompositionDto>())!;
        Assert.Equal("Approved", approved.Composition.Decision); Assert.Equal(factory.OwnerId, approved.Composition.ReviewedByOwnerId);
        Assert.Equal(created.Composition.ContentSha256, approved.Composition.ContentSha256); Assert.Equal(created.Content.PlainText, approved.Content.PlainText);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var stored = Assert.Single(await db.Compositions.ToArrayAsync());
        Assert.Equal(stored.ContentSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored.ContentJson))));
        Assert.Equal(source.Draft.SnapshotJson, (await db.PreparedDrafts.SingleAsync()).SnapshotJson);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task ConcurrentCompositionsAndOppositeReviews_ShouldKeepOneContentAndOneDecision()
    {
        var id = (await Seed()).Draft.Id;
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsync(Route(id), null)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Equal(5, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var documents = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<CompositionWriteResult>()));
        var hash = Assert.Single(documents.Select(x => x!.Document.Composition.ContentSha256).Distinct());
        Assert.Single(documents.Select(x => x!.Document.Composition.CreatedAtUtc).Distinct());
        foreach (var response in responses) response.Dispose();
        var reviews = await Task.WhenAll(client.PostAsJsonAsync(Route(id) + "/review", new CompositionReviewCommand(hash, "Approved", true)),
            client.PostAsJsonAsync(Route(id) + "/review", new CompositionReviewCommand(hash, "Rejected", true)));
        Assert.Single(reviews, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(reviews, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in reviews) response.Dispose();
        var winner = (await client.GetFromJsonAsync<CompositionDto>(Route(id)))!;
        using var retry = await client.PostAsJsonAsync(Route(id) + "/review", new CompositionReviewCommand(hash, winner.Composition.Decision, true)); retry.EnsureSuccessStatusCode();
        Assert.Equal(winner.Composition.ReviewedAtUtc, (await retry.Content.ReadFromJsonAsync<CompositionDto>())!.Composition.ReviewedAtUtc);
        Assert.Equal(winner.Composition.Decision, (await Compose(id)).Composition.Decision);
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("confirmation")]
    [InlineData("decision")]
    [InlineData("note")]
    [InlineData("format")]
    public async Task InvalidReview_ShouldLeaveContentPending(string invalid)
    {
        var id = (await Seed()).Draft.Id; var document = await Compose(id);
        var command = new CompositionReviewCommand(document.Composition.ContentSha256, "Approved", true);
        command = invalid switch { "hash" => command with { ContentSha256 = new string('a', 64) }, "confirmation" => command with { Confirmed = false },
            "decision" => command with { Decision = "Unknown" }, "note" => command with { Note = new string('a', 1001) }, _ => command with { ContentSha256 = "bad hash" } };
        Assert.Equal(invalid == "hash" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(Route(id) + "/review", command)).StatusCode);
        Assert.Equal("Pending", (await client.GetFromJsonAsync<CompositionDto>(Route(id)))!.Composition.Decision);
    }
    [Fact]
    public async Task FinalReview_ShouldRejectChangedNoteAndPreserveUnicodeNoteOnRetry()
    {
        var id = (await Seed()).Draft.Id; var composition = await Compose(id);
        var command = new CompositionReviewCommand(composition.Composition.ContentSha256, "Rejected", true, new string('é', 1000));
        using var first = await client.PostAsJsonAsync(Route(id) + "/review", command); first.EnsureSuccessStatusCode();
        using var retry = await client.PostAsJsonAsync(Route(id) + "/review", command); retry.EnsureSuccessStatusCode();
        Assert.Equal(command.Note, (await retry.Content.ReadFromJsonAsync<CompositionDto>())!.Composition.ReviewNote);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route(id) + "/review", command with { Note = "Different" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route(id) + "/review", command with { Decision = "Approved" })).StatusCode);
    }
    [Fact]
    public async Task OwnerAndAnonymousScoping_ShouldProtectCompositionHistoryAndReview()
    {
        var own = (await Seed()).Draft.Id; var foreign = (await Seed(owner: Guid.NewGuid())).Draft.Id;
        var document = await Compose(own);
        foreach (var id in new[] { foreign, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(Route(id), null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route(id) + "/review", new CompositionReviewCommand(document.Composition.ContentSha256, "Approved", true))).StatusCode);
        }
        Assert.Single((await client.GetFromJsonAsync<CompositionSummary[]>("/api/v1/documents/compositions"))!);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route(own))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/documents/compositions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Route(own), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route(own) + "/review", new CompositionReviewCommand(document.Composition.ContentSha256, "Approved", true))).StatusCode);
    }
    [Fact]
    public async Task HistoryAndConstraints_ShouldKeepContentSummariesAndProtectTheSourceDraft()
    {
        var first = (await Seed()).Draft.Id; var second = (await Seed("CoverLetter")).Draft.Id;
        await Compose(first); await Compose(second);
        Assert.Single((await client.GetFromJsonAsync<CompositionSummary[]>("/api/v1/documents/compositions?page=1&pageSize=1"))!);
        Assert.Single((await client.GetFromJsonAsync<CompositionSummary[]>("/api/v1/documents/compositions?page=2&pageSize=1"))!);
        Assert.Empty((await client.GetFromJsonAsync<CompositionSummary[]>("/api/v1/documents/compositions?page=3&pageSize=1"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/documents/compositions?page=0")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.PreparedDrafts.Where(x => x.Id == first).ExecuteDeleteAsync());
        var uncomposed = (await Seed()).Draft;
        db.Compositions.Add(DocumentComposition.Create(Guid.NewGuid(), uncomposed.Id, "Resume", VerifiedDocumentComposer.Version,
            uncomposed.SnapshotSha256, "{}", DateTime.UtcNow));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(failure.InnerException).SqlState);
    }
    [Fact]
    public async Task CorruptedSourceHash_ShouldNotProduceContent()
    {
        var id = (await Seed()).Draft.Id;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await db.PreparedDrafts.Where(x => x.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SnapshotJson, "{}"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync(Route(id), null)).StatusCode);
        Assert.Empty(await db.Compositions.ToArrayAsync());
    }
}
