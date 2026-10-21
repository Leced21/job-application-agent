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

public sealed class DocumentExportApiTests : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private readonly DocumentsApiFactory factory; private readonly WebApplicationFactory<Program> host; private readonly HttpClient client;
    public DocumentExportApiTests(DocumentsApiFactory factory)
    {
        this.factory = factory; host = factory.WithWebHostBuilder(_ => { }); client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private static string Route(Guid draft) => $"/api/v1/documents/drafts/{draft}/exports";
    private async Task<DocumentComposition> Seed(string kind = "Resume", string? decision = "Approved", Guid? owner = null, bool markup = false)
    {
        var sample = CompositionSamples.Create(kind, owner ?? factory.OwnerId, dates: true);
        var content = VerifiedDocumentComposer.Compose(sample.Draft);
        if (markup) content = content with { PlainText = content.PlainText + "\n<script>alert(1)</script> & données" };
        var composition = DocumentComposition.Create(sample.Draft.OwnerId, sample.Draft.Id, kind, VerifiedDocumentComposer.Version,
            sample.Draft.SnapshotSha256, JsonSerializer.Serialize(content), DateTime.UtcNow);
        if (decision is not null) composition.Review(sample.Draft.OwnerId, composition.ContentSha256, decision, "Checked", DateTime.UtcNow);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(sample.Resume); db.Extractions.Add(sample.Extraction); db.CandidateFacts.AddRange(sample.Facts); db.PreparedDrafts.Add(sample.Draft); db.Compositions.Add(composition);
        await db.SaveChangesAsync(); return composition;
    }
    private async Task<DocumentExportMetadata> Export(DocumentComposition composition, string format)
    {
        using var response = await client.PostAsJsonAsync(Route(composition.DraftId), new CreateDocumentExportCommand(format, composition.ContentSha256));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<DocumentExportWriteResult>())!.Export;
    }
    [Theory]
    [InlineData("Resume", "Pdf")]
    [InlineData("Resume", "Docx")]
    [InlineData("CoverLetter", "Pdf")]
    [InlineData("CoverLetter", "Docx")]
    public async Task ApprovedContent_ShouldExportPersistAndPreviewWithoutRegenerating(string kind, string format)
    {
        var composition = await Seed(kind);
        using var response = await client.PostAsJsonAsync(Route(composition.DraftId), new CreateDocumentExportCommand(format, composition.ContentSha256));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var metadata = (await response.Content.ReadFromJsonAsync<DocumentExportWriteResult>())!.Export;
        Assert.Equal(metadata, await client.GetFromJsonAsync<DocumentExportMetadata>(response.Headers.Location));
        Assert.Equal(metadata, await Export(composition, format));
        using var download = await client.GetAsync($"/api/v1/documents/exports/{metadata.Id}/content"); download.EnsureSuccessStatusCode();
        var bytes = await download.Content.ReadAsByteArrayAsync(); Assert.Equal(metadata.Length, bytes.Length);
        Assert.Equal(metadata.FileSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType); Assert.Equal(metadata.MediaType, download.Content.Headers.ContentType!.MediaType);
        Assert.True(download.Headers.CacheControl!.NoStore); Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        using var preview = await client.GetAsync($"/api/v1/documents/exports/{metadata.Id}/preview"); preview.EnsureSuccessStatusCode();
        if (format == "Pdf") { Assert.Equal(bytes, await preview.Content.ReadAsByteArrayAsync()); Assert.Equal("inline", preview.Content.Headers.ContentDisposition!.DispositionType); }
        else { Assert.Contains("pagination", await preview.Content.ReadAsStringAsync()); Assert.True(preview.Headers.Contains("Content-Security-Policy")); }
        var qaDirectory = Environment.GetEnvironmentVariable("JOB_AGENT_EXPORT_QA_DIR");
        if (!string.IsNullOrWhiteSpace(qaDirectory))
        { Directory.CreateDirectory(qaDirectory); await System.IO.File.WriteAllBytesAsync(Path.Combine(qaDirectory, $"sample-{kind.ToLowerInvariant()}.{format.ToLowerInvariant()}"), bytes); }
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Single(await db.Exports.ToArrayAsync());
    }
    [Theory]
    [InlineData(null)]
    [InlineData("Rejected")]
    public async Task UnapprovedComposition_ShouldNeverProduceFiles(string? decision)
    {
        var composition = await Seed(decision: decision);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route(composition.DraftId), new CreateDocumentExportCommand("Pdf", composition.ContentSha256))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<DocumentExportMetadata[]>("/api/v1/documents/exports"))!);
    }
    [Theory]
    [InlineData("hash")]
    [InlineData("format")]
    [InlineData("bad-hash")]
    public async Task WrongHashOrFormat_ShouldFailBeforeRendering(string invalid)
    {
        var composition = await Seed(); var command = new CreateDocumentExportCommand("Pdf", composition.ContentSha256);
        command = invalid switch { "hash" => command with { ContentSha256 = new string('a', 64) }, "format" => command with { Format = "Html" }, _ => command with { ContentSha256 = "invalid" } };
        Assert.Equal(invalid == "hash" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route(composition.DraftId), command)).StatusCode);
    }
    [Fact]
    public async Task OwnerAndAnonymousScoping_ShouldProtectEveryExportOperation()
    {
        var composition = await Seed(); var metadata = await Export(composition, "Pdf"); var other = await Seed(owner: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route(other.DraftId), new CreateDocumentExportCommand("Pdf", other.ContentSha256))).StatusCode);
        using var anonymous = host.CreateClient();
        foreach (var route in new[] { "/api/v1/documents/exports", $"/api/v1/documents/exports/{metadata.Id}", $"/api/v1/documents/exports/{metadata.Id}/content", $"/api/v1/documents/exports/{metadata.Id}/preview" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route(composition.DraftId), new CreateDocumentExportCommand("Pdf", composition.ContentSha256))).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var hidden = DocumentExport.Create(other.OwnerId, other.DraftId, "Pdf", "test", other.ContentSha256, [1], 1, other.ReviewedAtUtc!.Value, DateTime.UtcNow);
        db.Exports.Add(hidden); await db.SaveChangesAsync();
        foreach (var tail in new[] { "", "/content", "/preview" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/exports/{hidden.Id}{tail}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<DocumentExportMetadata[]>("/api/v1/documents/exports"))!);
    }
    [Theory]
    [InlineData("Pdf")]
    [InlineData("Docx")]
    public async Task ConcurrentRequests_ShouldConvergeToOneStoredArtifact(string format)
    {
        var composition = await Seed(); var command = new CreateDocumentExportCommand(format, composition.ContentSha256);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync(Route(composition.DraftId), command)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable }));
        var stored = await Export(composition, format);
        foreach (var response in responses.Where(x => x.IsSuccessStatusCode)) Assert.Equal(stored.Id, (await response.Content.ReadFromJsonAsync<DocumentExportWriteResult>())!.Export.Id);
        foreach (var response in responses) response.Dispose();
        using var scope = factory.Services.CreateScope(); Assert.Single(await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Exports.ToArrayAsync());
    }
    [Fact]
    public async Task FileTamperingAndChangedApproval_ShouldBlockDownloadAndPreview()
    {
        var composition = await Seed(); var metadata = await Export(composition, "Pdf");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var file = await db.Exports.AsNoTracking().SingleAsync(); var changed = file.Content.ToArray(); changed[10] ^= 1;
        await db.Exports.Where(x => x.Id == metadata.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Content, changed));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/v1/documents/exports/{metadata.Id}/content")).StatusCode);
        await db.Compositions.Where(x => x.DraftId == composition.DraftId).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Decision, "Rejected"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/v1/documents/exports/{metadata.Id}/preview")).StatusCode);
    }
    [Fact]
    public async Task DocxPreview_ShouldEscapeSourceMarkupAndHistoryShouldProtectComposition()
    {
        var composition = await Seed(markup: true); var metadata = await Export(composition, "Docx"); await Export(composition, "Pdf");
        var html = await client.GetStringAsync($"/api/v1/documents/exports/{metadata.Id}/preview");
        Assert.DoesNotContain("<script>", html); Assert.Contains("&lt;script&gt;", html);
        Assert.Single((await client.GetFromJsonAsync<DocumentExportMetadata[]>("/api/v1/documents/exports?page=2&pageSize=1"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/documents/exports?page=0")).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Compositions.Where(x => x.DraftId == composition.DraftId).ExecuteDeleteAsync());
    }
}
