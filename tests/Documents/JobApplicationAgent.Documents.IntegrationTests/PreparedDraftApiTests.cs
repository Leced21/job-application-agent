extern alias JobSource;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using JobDb = JobApplicationAgent.Job.Infrastructure.Persistence.JobDbContext;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class DraftJobApiFactory : WebApplicationFactory<JobSource::Program>, IAsyncLifetime
{
    private const string LocalToken = "documents-job-test-local-token-at-least-32-characters-long";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").WithDatabase("draft_jobs")
        .WithUsername("test_" + Guid.NewGuid().ToString("N")[..8]).WithPassword(Guid.NewGuid().ToString("N")).Build();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobDatabase"] = postgres.GetConnectionString(), ["Job:LocalAuth:OwnerId"] = OwnerId.ToString(),
            ["Job:LocalAuth:Token"] = LocalToken, ["Job:Admin:OwnerIds"] = OwnerId.ToString(),
            ["Job:CollectionPublisher:Token"] = "documents-job-test-publisher-token-at-least-32-characters-long"
        }));
    }
    public new HttpClient CreateClient()
    {
        var client = base.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", LocalToken); return client;
    }
    public async Task InitializeAsync()
    { await postgres.StartAsync(); using var scope = Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<JobDb>().Database.MigrateAsync(); }
    public async Task ResetAsync()
    { using var scope = Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<JobDb>().Jobs.ExecuteDeleteAsync(); }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
}

public sealed class PreparedDraftApiTests : IClassFixture<DocumentsApiFactory>, IClassFixture<DraftJobApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/documents/drafts";
    private readonly DocumentsApiFactory documents;
    private readonly DraftJobApiFactory jobs;
    private WebApplicationFactory<Program> host = null!;
    private HttpClient client = null!;
    private bool offline;
    private HttpStatusCode? fakeStatus;
    private string? fakeBody;
    private int jobReads;
    public PreparedDraftApiTests(DocumentsApiFactory documents, DraftJobApiFactory jobs) { this.documents = documents; this.jobs = jobs; }
    public Task InitializeAsync()
    {
        host = documents.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Documents:Drafts:JobBaseUrl"] = "http://job-api:8080" }));
            builder.ConfigureServices(services => services.AddHttpClient<IDraftJobClient, DraftJobClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new InterceptJobHandler(jobs.Server.CreateHandler(), () =>
                {
                    Interlocked.Increment(ref jobReads);
                    if (offline) throw new HttpRequestException("Simulated outage");
                    return fakeStatus is { } status ? new HttpResponseMessage(status) { Content = new StringContent(fakeBody ?? "{}") } : null;
                })));
        });
        client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", documents.Token);
        return Task.CompletedTask;
    }
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await documents.ResetAsync(); await jobs.ResetAsync(); }
    private async Task<(ResumeDocument Resume, ResumeCandidateFact[] Facts)> Source(string? decision = "Approved", Guid? owner = null, bool versioned = false)
    {
        var resume = ResumeDocument.Create(owner ?? documents.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, "SQL Azure")])), DateTime.UtcNow);
        var facts = new[] { CandidateFactBuilder.Create(extraction, new("Skill", [new("Name", 1, 0, 3)]), DateTime.UtcNow),
            CandidateFactBuilder.Create(extraction, new("Skill", [new("Name", 1, 4, 5)]), DateTime.UtcNow) };
        if (decision is not null) foreach (var fact in facts) fact.Review(resume.OwnerId, decision, "Checked", DateTime.UtcNow);
        using var scope = documents.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(resume); db.Extractions.Add(extraction); db.CandidateFacts.AddRange(facts);
        if (versioned)
        {
            var series = ResumeSeries.Create(resume.OwnerId, "Backend", DateTime.UtcNow); db.ResumeSeries.Add(series);
            db.ResumeVersions.Add(ResumeSeriesVersion.Create(resume.OwnerId, series.Id, resume.Id, 1, "Initial", DateTime.UtcNow));
        }
        await db.SaveChangesAsync(); return (resume, facts);
    }
    private static object JobBody(string title = "Engineer", string description = "SQL and Rust required") => new
    { title, companyName = "Acme", location = "Paris", workMode = "Remote", contractType = "Permanent", description, source = "Manual" };
    private async Task<Guid> Job()
    {
        using var receiver = jobs.CreateClient(); using var response = await receiver.PostAsJsonAsync("/api/v1/jobs", JobBody());
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static PrepareDraftCommand Command(Guid job, ResumeDocument resume, ResumeCandidateFact[] facts, string kind = "Resume") =>
        new(Guid.NewGuid(), job, resume.Id, kind, facts.Reverse().Select(x => x.Id).ToArray());
    [Theory]
    [InlineData("Resume")]
    [InlineData("CoverLetter")]
    public async Task Preparation_ShouldFreezeOrderedApprovedEvidenceAndRealJobWithoutInventingContent(string kind)
    {
        var source = await Source(versioned: true); var job = await Job(); var command = Command(job, source.Resume, source.Facts, kind);
        using var response = await client.PostAsJsonAsync(Route, command);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var prepared = (await response.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!.Document;
        Assert.Equal("PreparedForReview", prepared.Draft.Status); Assert.Equal(kind, prepared.Draft.Kind);
        Assert.Equal("Engineer", prepared.Snapshot.Job.Title); Assert.Contains("Rust", prepared.Snapshot.Job.Description);
        Assert.Equal(command.FactIds, prepared.Snapshot.Candidate.Facts.Select(x => x.Fact.Id));
        Assert.Equal(new[] { "Azure", "SQL" }, prepared.Snapshot.Candidate.Facts.Select(x => x.Fact.Fields[0].Value));
        Assert.Equal(source.Resume.Sha256, prepared.Snapshot.Candidate.Resume.Sha256); Assert.Equal(1, prepared.Snapshot.Candidate.Version!.VersionNumber);
        var read = (await client.GetFromJsonAsync<PreparedDraftDto>(response.Headers.Location))!;
        Assert.Equal(JsonSerializer.Serialize(prepared), JsonSerializer.Serialize(read)); Assert.True(response.Headers.CacheControl!.NoStore);
        using var scope = documents.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var stored = Assert.Single(await db.PreparedDrafts.ToArrayAsync());
        Assert.Equal(stored.SnapshotSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stored.SnapshotJson))));
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Equal(2, await db.CandidateFacts.CountAsync());
    }
    [Fact]
    public async Task Retry_ShouldPreserveSnapshotAfterJobChangeDeletionAndOutage()
    {
        var source = await Source(); var job = await Job(); var command = Command(job, source.Resume, source.Facts);
        using var created = await client.PostAsJsonAsync(Route, command); created.EnsureSuccessStatusCode();
        var first = (await created.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!.Document;
        using var receiver = jobs.CreateClient();
        (await receiver.PutAsJsonAsync($"/api/v1/jobs/{job}", JobBody("Changed title"))).EnsureSuccessStatusCode();
        (await receiver.DeleteAsync($"/api/v1/jobs/{job}")).EnsureSuccessStatusCode(); offline = true;
        using var retry = await client.PostAsJsonAsync(Route, command); Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var recovered = (await retry.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!;
        Assert.False(recovered.Created); Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(recovered.Document)); Assert.Equal(1, jobReads);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { Kind = "CoverLetter" })).StatusCode);
    }
    [Fact]
    public async Task ConcurrentIdenticalRequests_ShouldReturnOneDraft()
    {
        var source = await Source(); var command = Command(await Job(), source.Resume, source.Facts);
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync(Route, command)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created); Assert.Equal(5, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var results = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<PreparedDraftWriteResult>()));
        Assert.Single(results.Select(x => x!.Document.Draft.Id).Distinct()); Assert.Single(results.Select(x => x!.Document.Draft.SnapshotSha256).Distinct());
        foreach (var response in responses) response.Dispose();
    }
    [Theory]
    [InlineData(null)]
    [InlineData("Rejected")]
    public async Task UnapprovedFacts_ShouldBeRejectedBeforeReadingJob(string? decision)
    {
        var source = await Source(decision);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route, Command(Guid.NewGuid(), source.Resume, source.Facts))).StatusCode);
        Assert.Equal(0, jobReads);
    }
    [Theory]
    [InlineData("request")]
    [InlineData("job")]
    [InlineData("resume")]
    [InlineData("kind")]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("too-many")]
    public async Task InvalidSelection_ShouldPersistNothing(string invalid)
    {
        var source = await Source(); var command = Command(Guid.NewGuid(), source.Resume, source.Facts);
        command = invalid switch {
            "request" => command with { RequestId = Guid.Empty }, "job" => command with { JobId = Guid.Empty },
            "resume" => command with { ResumeId = Guid.Empty }, "kind" => command with { Kind = "Unknown" },
            "empty" => command with { FactIds = [] }, "duplicate" => command with { FactIds = [source.Facts[0].Id, source.Facts[0].Id] },
            _ => command with { FactIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray() } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, command)).StatusCode); Assert.Equal(0, jobReads);
    }
    [Fact]
    public async Task OwnershipResumeBindingAndAnonymousAccess_ShouldProtectEveryDraftOperation()
    {
        var own = await Source(); var other = await Source(); var foreign = await Source(owner: Guid.NewGuid()); var job = await Job();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route, Command(job, own.Resume, other.Facts))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route, Command(job, foreign.Resume, foreign.Facts))).StatusCode);
        using var created = await client.PostAsJsonAsync(Route, Command(job, own.Resume, own.Facts)); created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!.Document.Draft;
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Route}/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route, Command(job, own.Resume, own.Facts))).StatusCode);
        using var scope = documents.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await db.PreparedDrafts.Where(x => x.Id == draft.Id).ExecuteDeleteAsync();
        var hidden = PreparedDocumentDraft.Create(foreign.Resume.OwnerId, draft.RequestId, foreign.Resume.Id, job, "Resume", new string('a', 64), "{}", DateTime.UtcNow);
        db.PreparedDrafts.Add(hidden); await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{hidden.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PreparedDraftSummary[]>(Route))!);
    }
    [Theory]
    [InlineData("Ignored")]
    [InlineData("Expired")]
    public async Task InactiveJob_ShouldNotProduceADraft(string status)
    {
        var source = await Source(); var id = await Job();
        using var scope = jobs.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<JobDb>();
        var value = Enum.Parse<JobApplicationAgent.Job.Domain.Enums.JobStatus>(status);
        await db.Jobs.Where(x => x.Id == id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, value));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route, Command(id, source.Resume, source.Facts))).StatusCode);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("offline")]
    [InlineData("redirect")]
    [InlineData("json")]
    [InlineData("oversize")]
    [InlineData("wrong-id")]
    public async Task MissingOrInvalidJobResponse_ShouldNeverPersistADraft(string failure)
    {
        var source = await Source(); var id = Guid.NewGuid();
        if (failure == "offline") offline = true;
        if (failure == "redirect") fakeStatus = HttpStatusCode.Redirect;
        if (failure == "json") { fakeStatus = HttpStatusCode.OK; fakeBody = "not json"; }
        if (failure == "oversize") { fakeStatus = HttpStatusCode.OK; fakeBody = new string('a', 131073); }
        if (failure == "wrong-id")
        {
            fakeStatus = HttpStatusCode.OK; fakeBody = JsonSerializer.Serialize(new DraftJobSnapshot(Guid.NewGuid(), "Engineer", "Acme", null, null, "Manual", null, "Discovered", DateTime.UtcNow));
        }
        Assert.Equal(failure == "missing" ? HttpStatusCode.NotFound : HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync(Route, Command(id, source.Resume, source.Facts))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PreparedDraftSummary[]>(Route))!);
    }
    [Fact]
    public async Task SelectionOrderChanges_ShouldRequireANewRequestIdAndHistoryShouldContainOnlySummaries()
    {
        var source = await Source(); var command = Command(await Job(), source.Resume, source.Facts);
        using var first = await client.PostAsJsonAsync(Route, command); first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { FactIds = command.FactIds.Reverse().ToArray() })).StatusCode);
        using var second = await client.PostAsJsonAsync(Route, command with { RequestId = Guid.NewGuid(), FactIds = [source.Facts[0].Id] }); second.EnsureSuccessStatusCode();
        var selected = (await second.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!.Document;
        Assert.Single(selected.Snapshot.Candidate.Facts); Assert.Null(selected.Snapshot.Candidate.Version);
        var page = (await client.GetFromJsonAsync<PreparedDraftSummary[]>(Route + "?page=1&pageSize=1"))!;
        Assert.Equal(selected.Draft.Id, Assert.Single(page).Id);
        Assert.Single((await client.GetFromJsonAsync<PreparedDraftSummary[]>(Route + "?page=2&pageSize=1"))!);
        Assert.Empty((await client.GetFromJsonAsync<PreparedDraftSummary[]>(Route + "?page=3&pageSize=1"))!);
        foreach (var query in new[] { "?page=0", "?pageSize=101" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + query)).StatusCode);
    }
    [Fact]
    public async Task ConfirmedDates_ShouldRetainOwnerProvenanceSeparatelyFromSourcePeriod()
    {
        var resume = ResumeDocument.Create(documents.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, "Engineer Acme 2020-2024")])), DateTime.UtcNow);
        var fact = CandidateFactBuilder.Create(extraction, new("Experience", [new("Title", 1, 0, 8), new("Company", 1, 9, 4), new("Period", 1, 14, 9)]), DateTime.UtcNow);
        fact.Review(documents.OwnerId, "Approved", null, DateTime.UtcNow);
        using (var scope = documents.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>(); db.Resumes.Add(resume); db.Extractions.Add(extraction); db.CandidateFacts.Add(fact); await db.SaveChangesAsync();
        }
        var dates = new ConfirmProfileDatesCommand(new(2020, 3, 12), new(2024, 7, 19), false, null, null, true, "Exact dates checked separately");
        (await client.PostAsJsonAsync($"/api/v1/documents/resumes/{resume.Id}/facts/{fact.Id}/profile-mapping", dates)).EnsureSuccessStatusCode();
        using var response = await client.PostAsJsonAsync(Route, Command(await Job(), resume, [fact])); response.EnsureSuccessStatusCode();
        var snapshot = Assert.Single((await response.Content.ReadFromJsonAsync<PreparedDraftWriteResult>())!.Document.Snapshot.Candidate.Facts);
        Assert.Equal(dates, snapshot.Dates); Assert.Equal(documents.OwnerId, snapshot.DatesConfirmedByOwnerId); Assert.NotNull(snapshot.DatesConfirmedAtUtc);
        Assert.Equal("2020-2024", snapshot.Fact.Fields.Single(x => x.Field == "Period").Value);
        Assert.Equal("OwnerConfirmed", snapshot.DatesOrigin);
    }
    [Theory]
    [InlineData("http://external.example.test")]
    [InlineData("https://external.example.test")]
    [InlineData("http://another-api:8080")]
    [InlineData("http://job-api:8081")]
    [InlineData("http://user:password@localhost:5002")]
    public async Task InvalidJobBaseUrl_ShouldNeverSendARequest(string address)
    {
        var source = await Source();
        using var invalid = host.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Documents:Drafts:JobBaseUrl"] = address })));
        using var invalidClient = invalid.CreateClient(); invalidClient.DefaultRequestHeaders.Authorization = new("Bearer", documents.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await invalidClient.PostAsJsonAsync(Route, Command(Guid.NewGuid(), source.Resume, source.Facts))).StatusCode);
        Assert.Equal(0, jobReads);
    }
    [Fact]
    public async Task DraftForeignKey_ShouldProtectSourceAndRejectAnotherOwner()
    {
        var source = await Source(); var command = Command(await Job(), source.Resume, source.Facts);
        (await client.PostAsJsonAsync(Route, command)).EnsureSuccessStatusCode();
        using var scope = documents.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Resumes.Where(x => x.Id == source.Resume.Id).ExecuteDeleteAsync());
        var invalid = PreparedDocumentDraft.Create(Guid.NewGuid(), Guid.NewGuid(), source.Resume.Id, command.JobId, "Resume", new string('a', 64), "{}", DateTime.UtcNow);
        db.PreparedDrafts.Add(invalid);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(failure.InnerException).SqlState);
    }
    private sealed class InterceptJobHandler(HttpMessageHandler inner, Func<HttpResponseMessage?> intercept) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Assert.Null(request.Headers.Authorization); return intercept() is { } response ? Task.FromResult(response) : base.SendAsync(request, ct); }
    }
}
