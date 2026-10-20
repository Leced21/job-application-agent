using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class ResumeSeriesApiTests : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/documents/resume-series";
    private readonly DocumentsApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private readonly HttpClient client;
    public ResumeSeriesApiTests(DocumentsApiFactory factory)
    {
        this.factory = factory; host = factory.WithWebHostBuilder(_ => { }); client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private async Task<ResumeMetadata> Upload(bool docx = false)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(docx ? ResumeSamples.Docx() : ResumeSamples.Pdf());
        part.Headers.ContentType = new MediaTypeHeaderValue(docx ? ResumeFileValidator.DocxType : "application/pdf");
        form.Add(part, "file", docx ? "cv.docx" : "cv.pdf");
        using var response = await client.PostAsync("/api/v1/documents/resumes", form); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResumeMetadata>())!;
    }
    private async Task<ResumeSeriesWriteResult> Create(Guid first, string name = "CV backend", string? label = null)
    {
        using var response = await client.PostAsJsonAsync(Route, new CreateResumeSeriesCommand(name, first, label));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ResumeSeriesWriteResult>())!;
    }
    [Fact]
    public async Task History_ShouldPreserveBothFilesAndKeepReviewBoundToItsOriginalVersion()
    {
        var first = await Upload(); var second = await Upload(true);
        ResumeCandidateFact fact;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            var document = await db.Resumes.SingleAsync(x => x.Id == first.Id);
            var extraction = ResumeTextExtraction.Create(document, ExtractedText.Version,
                JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, "SQL")])), DateTime.UtcNow);
            fact = CandidateFactBuilder.Create(extraction, new("Skill", [new("Name", 1, 0, 3)]), DateTime.UtcNow);
            fact.Review(factory.OwnerId, "Approved", null, DateTime.UtcNow);
            db.Extractions.Add(extraction); db.CandidateFacts.Add(fact); await db.SaveChangesAsync();
        }
        var series = await Create(first.Id, "  CV backend  ", "Initial");
        Assert.Equal("CV backend", series.Series.Name); Assert.Equal(1, series.Version.VersionNumber);
        using var added = await client.PostAsJsonAsync($"{Route}/{series.Series.Id}/versions", new AddResumeVersionCommand(second.Id, "Updated"));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var result = (await added.Content.ReadFromJsonAsync<ResumeSeriesWriteResult>())!;
        Assert.Equal(2, result.Series.VersionCount); Assert.Equal(2, result.Version.VersionNumber);
        Assert.Equal(result.Version, await client.GetFromJsonAsync<ResumeSeriesVersionMetadata>(added.Headers.Location));
        var history = (await client.GetFromJsonAsync<ResumeSeriesVersionMetadata[]>($"{Route}/{series.Series.Id}/versions"))!;
        Assert.Equal(new[] { 2, 1 }, history.Select(x => x.VersionNumber));
        Assert.Equal(first, history[1].Resume); Assert.Equal(second, history[0].Resume);
        Assert.Equal(series.Series.Id, (await client.GetFromJsonAsync<ResumeSeriesSummary>($"/api/v1/documents/resumes/{second.Id}/series"))!.Id);
        using var pdf = await client.GetAsync($"/api/v1/documents/resumes/{first.Id}/content");
        using var docx = await client.GetAsync($"/api/v1/documents/resumes/{second.Id}/content");
        Assert.Equal(ResumeSamples.Pdf(), await pdf.Content.ReadAsByteArrayAsync());
        Assert.Equal(second.Sha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(await docx.Content.ReadAsByteArrayAsync())));
        var approved = await client.GetFromJsonAsync<CandidateFactDto>($"/api/v1/documents/resumes/{first.Id}/facts/{fact.Id}");
        Assert.Equal("Approved", approved!.Decision); Assert.Equal(first.Sha256, approved.SourceSha256);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/resumes/{second.Id}/extraction")).StatusCode);
        Assert.True(added.Headers.CacheControl!.NoStore);
        using var check = factory.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.Single(await context.CandidateFacts.ToArrayAsync()); Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task ConcurrentInitialRequests_ShouldCreateOneSeriesAndPreserveItsFirstTimestamp()
    {
        var resume = await Upload(); var command = new CreateResumeSeriesCommand("Backend", resume.Id, "First");
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route, command)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var results = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<ResumeSeriesWriteResult>()));
        Assert.Single(results.Select(x => x!.Series.Id).Distinct()); Assert.Single(results.Select(x => x!.Version.AddedAtUtc).Distinct());
        foreach (var response in responses) response.Dispose();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { Name = "Other name" })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.Single(await db.ResumeSeries.ToArrayAsync()); Assert.Single(await db.ResumeVersions.ToArrayAsync());
    }
    [Fact]
    public async Task ConcurrentAppendsAndRetries_ShouldAllocateDistinctConsecutiveNumbers()
    {
        var first = await Upload(); var series = await Create(first.Id);
        var documents = new[] { await Upload(), await Upload(), await Upload() };
        var path = $"{Route}/{series.Series.Id}/versions";
        var responses = await Task.WhenAll(documents.SelectMany(x => new[] {
            client.PostAsJsonAsync(path, new AddResumeVersionCommand(x.Id)), client.PostAsJsonAsync(path, new AddResumeVersionCommand(x.Id)) }));
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Created)); Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        foreach (var response in responses) response.Dispose();
        var history = (await client.GetFromJsonAsync<ResumeSeriesVersionMetadata[]>($"{path}?page=1&pageSize=2"))!;
        Assert.Equal(new[] { 4, 3 }, history.Select(x => x.VersionNumber));
        var older = (await client.GetFromJsonAsync<ResumeSeriesVersionMetadata[]>($"{path}?page=2&pageSize=2"))!;
        Assert.Equal(new[] { 2, 1 }, older.Select(x => x.VersionNumber));
        Assert.Empty((await client.GetFromJsonAsync<ResumeSeriesVersionMetadata[]>($"{path}?page=3&pageSize=2"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new AddResumeVersionCommand(first.Id, "Changed label"))).StatusCode);
    }
    [Fact]
    public async Task CompetingSeries_ShouldNeverAssignTheSameDocumentTwice()
    {
        var a = await Create((await Upload()).Id); var b = await Create((await Upload()).Id); var shared = await Upload();
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"{Route}/{a.Series.Id}/versions", new AddResumeVersionCommand(shared.Id)),
            client.PostAsJsonAsync($"{Route}/{b.Series.Id}/versions", new AddResumeVersionCommand(shared.Id)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.Equal(1, await db.ResumeVersions.CountAsync(x => x.ResumeId == shared.Id));
        Assert.Equal(2, (await client.GetFromJsonAsync<ResumeSeriesSummary[]>(Route))!.Length);
    }
    [Fact]
    public async Task OwnerScoping_ShouldProtectEverySeriesOperation()
    {
        var own = await Upload(); var series = await Create(own.Id);
        var foreignOwner = Guid.NewGuid();
        var foreignResume = ResumeDocument.Create(foreignOwner, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var foreignSeries = ResumeSeries.Create(foreignOwner, "Private", DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            db.Resumes.Add(foreignResume); db.ResumeSeries.Add(foreignSeries);
            db.ResumeVersions.Add(ResumeSeriesVersion.Create(foreignOwner, foreignSeries.Id, foreignResume.Id, 1, null, DateTime.UtcNow)); await db.SaveChangesAsync();
        }
        var path = $"{Route}/{foreignSeries.Id}";
        foreach (var route in new[] { path, path + "/versions", path + "/versions/1", $"/api/v1/documents/resumes/{foreignResume.Id}/series" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route, new CreateResumeSeriesCommand("Stolen", foreignResume.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/versions", new AddResumeVersionCommand(own.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Route}/{series.Series.Id}/versions", new AddResumeVersionCommand(foreignResume.Id))).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<ResumeSeriesSummary[]>(Route))!);
        using var anonymous = host.CreateClient();
        foreach (var route in new[] { Route, path, path + "/versions", path + "/versions/1", $"/api/v1/documents/resumes/{own.Id}/series" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route, new CreateResumeSeriesCommand("Private", own.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path + "/versions", new AddResumeVersionCommand(own.Id))).StatusCode);
    }
    [Theory]
    [InlineData("name")]
    [InlineData("long-name")]
    [InlineData("control-name")]
    [InlineData("label")]
    [InlineData("empty-id")]
    public async Task InvalidCreation_ShouldPersistNothing(string invalid)
    {
        var command = new CreateResumeSeriesCommand("Backend", (await Upload()).Id);
        command = invalid switch {
            "name" => command with { Name = " " }, "long-name" => command with { Name = new string('a', 201) },
            "control-name" => command with { Name = "A\nB" }, "label" => command with { Label = new string('a', 201) },
            _ => command with { FirstResumeId = Guid.Empty } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, command)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ResumeSeriesSummary[]>(Route))!);
    }
    [Fact]
    public async Task UnknownStandaloneAndInvalidPagination_ShouldHavePredictableResponses()
    {
        var resume = await Upload();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/documents/resumes/{resume.Id}/series")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route, new CreateResumeSeriesCommand("Missing", Guid.NewGuid()))).StatusCode);
        var series = await Create(resume.Id); var path = $"{Route}/{series.Series.Id}/versions";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/2")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/0")).StatusCode);
        foreach (var route in new[] { Route + "?page=0", Route + "?pageSize=101", path + "?pageSize=0", path + "?page=1000001" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(route)).StatusCode);
    }
    [Fact]
    public async Task SeriesLimit_ShouldRejectNewVersionButAcceptAnExistingRetry()
    {
        var first = await Upload(); var series = await Create(first.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            for (var number = 2; number <= 1000; number++)
            {
                var document = ResumeDocument.Create(factory.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
                db.Resumes.Add(document); db.ResumeVersions.Add(ResumeSeriesVersion.Create(factory.OwnerId, series.Series.Id, document.Id, number, null, DateTime.UtcNow));
            }
            await db.SaveChangesAsync();
        }
        var next = await Upload(); var path = $"{Route}/{series.Series.Id}/versions";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new AddResumeVersionCommand(next.Id))).StatusCode);
        using var retry = await client.PostAsJsonAsync(path, new AddResumeVersionCommand(first.Id));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1000, (await retry.Content.ReadFromJsonAsync<ResumeSeriesWriteResult>())!.Series.VersionCount);
    }
    [Fact]
    public async Task CompositeForeignKeys_ShouldRejectCrossOwnerMembershipAndProtectLinkedFiles()
    {
        var own = await Upload(); var series = await Create(own.Id);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var other = Guid.NewGuid(); var foreign = ResumeDocument.Create(other, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        db.Resumes.Add(foreign); await db.SaveChangesAsync();
        db.ResumeVersions.Add(ResumeSeriesVersion.Create(factory.OwnerId, series.Series.Id, foreign.Id, 2, null, DateTime.UtcNow));
        var violation = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(violation.InnerException).SqlState);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Resumes.Where(x => x.Id == own.Id).ExecuteDeleteAsync());
        Assert.True(await db.Resumes.AnyAsync(x => x.Id == own.Id));
    }
    [Fact]
    public async Task Migration_ShouldKeepExistingUploadsStandaloneAndPreserveTheirContent()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261015152633_AddConfirmedProfileDates");
        var document = ResumeDocument.Create(factory.OwnerId, "old-cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        db.Resumes.Add(document); await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Empty(await db.ResumeSeries.ToArrayAsync()); Assert.Empty(await db.ResumeVersions.ToArrayAsync());
        var saved = await db.Resumes.AsNoTracking().SingleAsync();
        Assert.Equal(document.Content, saved.Content); Assert.Equal(document.Sha256, saved.Sha256); Assert.Equal(document.UploadedAtUtc, saved.UploadedAtUtc);
        var series = await Create(document.Id); Assert.Equal(document.Id, series.Version.Resume.Id);
    }
}
