extern alias JobSource;
extern alias ProfileSource;
extern alias DocumentsSource;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Applications.IntegrationTests;

public sealed class ApplicationsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").WithDatabase("applications_tests")
        .WithUsername("test_" + Guid.NewGuid().ToString("N")[..8]).WithPassword(Guid.NewGuid().ToString("N")).Build();
    private WebApplicationFactory<JobSource::Program> jobs = null!;
    private WebApplicationFactory<ProfileSource::Program> profiles = null!;
    private WebApplicationFactory<DocumentsSource::Program> documents = null!;
    public HttpClient DocumentsClient { get; private set; } = null!;
    public string DocumentsOwnerToken { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    public string DocumentsChannelToken { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    public string ProfileServiceToken { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    public bool DocumentsUnavailable { get; set; }
    public HttpClient JobClient { get; private set; } = null!;
    public HttpClient ProfileClient { get; private set; } = null!;
    public Guid Owner { get; } = Guid.NewGuid();
    public Guid Profile { get; private set; }
    public string Token { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    public bool SourcesUnavailable { get; set; }
    public bool SawForwardedAuthorization { get; private set; }
    public bool SawForwardedCookie { get; private set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:ApplicationsDatabase"] = postgres.GetConnectionString(), ["Applications:LocalAuth:OwnerId"] = Owner.ToString(),
            ["Applications:LocalAuth:Token"] = Token, ["Applications:Sources:ProfileId"] = Profile.ToString(),
            ["Applications:Documents:Token"] = DocumentsChannelToken, ["Documents:Applications:Token"] = DocumentsChannelToken,
            ["Applications:Submission:Enabled"] = "true",
            ["Applications:Submission:Platforms:0:PlatformId"] = "synthetic-official-api",
            ["Applications:Submission:Platforms:0:HasOfficialApi"] = "true",
            ["Applications:Submission:Platforms:0:TermsAllowAutomation"] = "true",
            ["Applications:Submission:Platforms:0:RequiresCaptcha"] = "false",
            ["Applications:Submission:Platforms:0:SupportsIdempotencyKey"] = "true" }));
        builder.ConfigureServices(s => s.AddHttpClient<IApplicationSources, LocalApplicationSources>()
            .ConfigurePrimaryHttpMessageHandler(() => new SourceRouter(this)));
        builder.ConfigureServices(s => s.AddHttpClient<IApplicationDocumentClient, ApplicationDocumentClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new DocumentsRouter(this)));
    }
    public HttpClient Client(Guid? owner = null, bool authorized = true, TimeProvider? clock = null)
    {
        var isolated = WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Applications:LocalAuth:OwnerId"] = (owner ?? Owner).ToString() })));
        if (clock is not null) isolated = isolated.WithWebHostBuilder(b => b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton(clock))));
        var client = isolated.CreateClient();
        if (authorized) client.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        return client;
    }
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        jobs = new WebApplicationFactory<JobSource::Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:JobDatabase"] = postgres.GetConnectionString(), ["Job:LocalAuth:OwnerId"] = Owner.ToString(),
                ["Job:LocalAuth:Token"] = "applications-job-test-local-token-at-least-32-characters",
                ["Job:Admin:OwnerIds"] = Owner.ToString(),
                ["Job:CollectionPublisher:Token"] = "applications-job-test-publisher-token-at-least-32-characters" })));
        profiles = new WebApplicationFactory<ProfileSource::Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:ProfileDatabase"] = postgres.GetConnectionString(), ["Profile:LocalAuth:OwnerId"] = Owner.ToString(),
                ["Profile:LocalAuth:Token"] = ProfileServiceToken })));
        using (var scope = jobs.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<JobDbContext>().Database.MigrateAsync();
        using (var scope = profiles.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
        JobClient = jobs.CreateClient(); ProfileClient = profiles.CreateClient();
        JobClient.DefaultRequestHeaders.Authorization = new("Bearer", "applications-job-test-local-token-at-least-32-characters");
        ProfileClient.DefaultRequestHeaders.Authorization = new("Bearer", ProfileServiceToken);
        using var created = await ProfileClient.PostAsJsonAsync("/api/v1/profile", new { firstName = "Synthetic", lastName = "Candidate", email = "synthetic@example.test" });
        created.EnsureSuccessStatusCode();
        Profile = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        documents = new WebApplicationFactory<DocumentsSource::Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DocumentsDatabase"] = postgres.GetConnectionString(), ["Documents:LocalAuth:OwnerId"] = Owner.ToString(),
                ["Documents:LocalAuth:Token"] = DocumentsOwnerToken, ["Documents:Applications:OwnerId"] = Owner.ToString(),
                ["Documents:Applications:ProfileId"] = Profile.ToString(), ["Documents:Applications:Token"] = DocumentsChannelToken })));
        using (var scope = documents.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.MigrateAsync();
        DocumentsClient = documents.CreateClient(); DocumentsClient.DefaultRequestHeaders.Authorization = new("Bearer", DocumentsOwnerToken);
        using var appScope = Services.CreateScope();
        await appScope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Database.MigrateAsync();
    }
    public async Task ResetAsync()
    {
        SourcesUnavailable = false; DocumentsUnavailable = false;
        SawForwardedAuthorization = false; SawForwardedCookie = false;
        DocumentsClient.Dispose();
        DocumentsClient = documents.WithWebHostBuilder(_ => { }).CreateClient();
        DocumentsClient.DefaultRequestHeaders.Authorization = new("Bearer", DocumentsOwnerToken);
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        await db.Requests.ExecuteDeleteAsync(); await db.Candidacies.ExecuteDeleteAsync();
        using var docScope = documents.Services.CreateScope(); var docs = docScope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await docs.ApplicationBindings.ExecuteDeleteAsync(); await docs.Exports.ExecuteDeleteAsync(); await docs.Compositions.ExecuteDeleteAsync();
        await docs.PreparedDrafts.ExecuteDeleteAsync(); await docs.ResumeSeries.ExecuteDeleteAsync(); await docs.Resumes.ExecuteDeleteAsync();
        using var jobScope = jobs.Services.CreateScope(); await jobScope.ServiceProvider.GetRequiredService<JobDbContext>().Jobs.ExecuteDeleteAsync();
    }
    public async Task<Guid> NewJobAsync()
    {
        using var response = await JobClient.PostAsJsonAsync("/api/v1/jobs", new { title = "Engineer", companyName = "Acme",
            location = "Paris", workMode = "Remote", contractType = "Permanent", description = "SQL", source = "Manual" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    public async Task<DocumentExportMetadata> ExportAsync(Guid job, string kind = "Resume", bool bind = true)
    {
        var sample = CompositionSamples.Create(kind, Owner);
        var source = JsonSerializer.Deserialize<DraftSourceSnapshot>(sample.Draft.SnapshotJson)!;
        source = source with { Job = source.Job with { Id = job } };
        var draft = PreparedDocumentDraft.Create(Owner, Guid.NewGuid(), sample.Resume.Id, job, kind, new string('a', 64), JsonSerializer.Serialize(source), DateTime.UtcNow);
        using (var scope = documents.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            db.Resumes.Add(sample.Resume); db.Extractions.Add(sample.Extraction); db.CandidateFacts.AddRange(sample.Facts); db.PreparedDrafts.Add(draft);
            await db.SaveChangesAsync();
        }
        using var compose = await DocumentsClient.PostAsJsonAsync($"/api/v1/documents/drafts/{draft.Id}/composition", new { });
        compose.EnsureSuccessStatusCode();
        var hash = (await compose.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("document").GetProperty("composition").GetProperty("contentSha256").GetString()!;
        using var review = await DocumentsClient.PostAsJsonAsync($"/api/v1/documents/drafts/{draft.Id}/composition/review", new { contentSha256 = hash, decision = "Approved", confirmed = true });
        review.EnsureSuccessStatusCode();
        using var export = await DocumentsClient.PostAsJsonAsync($"/api/v1/documents/drafts/{draft.Id}/exports", new { format = "Docx", contentSha256 = hash });
        export.EnsureSuccessStatusCode();
        var metadata = (await export.Content.ReadFromJsonAsync<DocumentExportWriteResult>())!.Export;
        if (bind)
        {
            using var receipt = await DocumentsClient.PostAsJsonAsync($"/api/v1/documents/exports/{metadata.Id}/application-binding", new { profileId = Profile, fileSha256 = metadata.FileSha256, confirmed = true });
            receipt.EnsureSuccessStatusCode();
        }
        return metadata;
    }
    public async Task CorruptExportAsync(Guid exportId, string change)
    {
        using var scope = documents.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var file = await db.Exports.AsNoTracking().SingleAsync(x => x.Id == exportId);
        if (change == "approval") await db.Compositions.Where(x => x.DraftId == file.DraftId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Decision, "Rejected"));
        else { var bytes = file.Content.ToArray(); bytes[0] ^= 1; await db.Exports.Where(x => x.Id == exportId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Content, bytes)); }
    }
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync(); JobClient.Dispose(); ProfileClient.Dispose();
        DocumentsClient.Dispose(); await documents.DisposeAsync(); await jobs.DisposeAsync(); await profiles.DisposeAsync(); await postgres.DisposeAsync();
    }
    private sealed class DocumentsRouter(ApplicationsApiFactory f) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (f.DocumentsUnavailable) throw new HttpRequestException("Synthetic Documents outage");
            using var forwarded = new HttpRequestMessage(HttpMethod.Get, request.RequestUri!.PathAndQuery);
            if (request.Headers.Authorization?.Parameter != f.DocumentsChannelToken) throw new InvalidOperationException("Applications did not use the dedicated Documents channel token.");
            forwarded.Headers.Authorization = request.Headers.Authorization;
            return await f.DocumentsClient.SendAsync(forwarded, ct);
        }
    }
    private sealed class SourceRouter(ApplicationsApiFactory f) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Headers.Authorization is not null) f.SawForwardedAuthorization = true;
            if (request.Headers.Contains("Cookie")) f.SawForwardedCookie = true;
            if (f.SourcesUnavailable) throw new HttpRequestException("Synthetic outage");
            var client = request.RequestUri!.Port == 5001 ? f.ProfileClient : f.JobClient;
            return await client.GetAsync(request.RequestUri.PathAndQuery, ct);
        }
    }
}
