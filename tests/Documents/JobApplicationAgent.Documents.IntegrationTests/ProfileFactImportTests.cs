extern alias ProfileReceiver;
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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using ProfileDb = JobApplicationAgent.Profile.Infrastructure.Persistence.ProfileDbContext;
using CandidateProfile = JobApplicationAgent.Profile.Domain.Entities.CandidateProfile;
using Skill = JobApplicationAgent.Profile.Domain.Entities.Skill;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class ProfileImportApiFactory : WebApplicationFactory<ProfileReceiver::Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").WithDatabase("profile_imports")
        .WithUsername("test_" + Guid.NewGuid().ToString("N")[..8]).WithPassword(Guid.NewGuid().ToString("N")).Build();
    public Guid OwnerId { get; set; }
    public CandidateProfile Profile { get; } = new("Import", "Candidate", "import@example.test");
    public string Token { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProfileDatabase"] = postgres.GetConnectionString(),
            ["Profile:DocumentImport:Token"] = Token, ["Profile:DocumentImport:OwnerId"] = OwnerId.ToString(),
            ["Profile:DocumentImport:ProfileId"] = Profile.Id.ToString()
        }));
    }
    public async Task InitializeAsync() => await postgres.StartAsync();
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        await db.Database.MigrateAsync();
        await db.DocumentFactImports.ExecuteDeleteAsync();
        await db.CandidateProfiles.ExecuteDeleteAsync();
        db.CandidateProfiles.Add(Profile);
        await db.SaveChangesAsync();
    }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
}

public sealed class ProfileFactImportTests : IClassFixture<DocumentsApiFactory>, IClassFixture<ProfileImportApiFactory>, IAsyncLifetime
{
    private readonly DocumentsApiFactory documents;
    private readonly ProfileImportApiFactory profile;
    private WebApplicationFactory<Program> host = null!;
    private HttpClient client = null!;
    private bool loseNextPost;
    private int posts;
    public ProfileFactImportTests(DocumentsApiFactory documents, ProfileImportApiFactory profile)
    { this.documents = documents; this.profile = profile; }
    public async Task InitializeAsync()
    {
        profile.OwnerId = documents.OwnerId;
        await profile.ResetAsync();
        host = documents.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Documents:ProfileImport:Token"] = profile.Token,
                ["Documents:ProfileImport:ProfileId"] = profile.Profile.Id.ToString(),
                ["Documents:ProfileImport:BaseUrl"] = "http://localhost:5001"
            }));
            builder.ConfigureServices(services => services.AddHttpClient<IProfileFactImportClient, ProfileFactImportClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new LostResponseHandler(profile.Server.CreateHandler(), () =>
                {
                    posts++;
                    if (!loseNextPost) return false;
                    loseNextPost = false;
                    return true;
                })));
        });
        client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", documents.Token);
    }
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await documents.ResetAsync(); }
    private static string ImportPath(ResumeCandidateFact fact) => $"/api/v1/documents/resumes/{fact.ResumeId}/facts/{fact.Id}/profile-import";
    private async Task<ResumeCandidateFact> Seed(string kind = "Skill", string value = "SQL", string? decision = "Approved", Guid? owner = null)
    {
        var resume = ResumeDocument.Create(owner ?? documents.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, value)])), DateTime.UtcNow);
        var fact = CandidateFactBuilder.Create(extraction, new(kind, [new(kind == "Experience" ? "Title" : "Name", 1, 0, value.Length)]), DateTime.UtcNow);
        if (decision is not null) fact.Review(resume.OwnerId, decision, null, DateTime.UtcNow);
        using var scope = documents.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(resume); db.Extractions.Add(extraction); db.CandidateFacts.Add(fact);
        await db.SaveChangesAsync();
        return fact;
    }
    private async Task<ProfileFactReceipt> Import(ResumeCandidateFact fact)
    {
        using var response = await client.PostAsync(ImportPath(fact), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProfileFactReceipt>())!;
    }
    private static ConfirmProfileDatesCommand DatedCommand(string kind) => kind == "Certification"
        ? new(null, null, null, new(2021, 3, 12), new(2024, 7, 19), true, "Verified against the certificate")
        : new(new(2020, 3, 12), new(2024, 7, 19), false, null, null, true, "Exact dates verified against original records");
    private async Task<ResumeCandidateFact> SeedDated(string kind, bool confirm = true)
    {
        var fields = kind switch
        {
            "Experience" => new Dictionary<string, string> { ["Title"] = "Engineer", ["Company"] = "Acme", ["Period"] = "2020-2024", ["Description"] = "Source description" },
            "Education" => new() { ["Degree"] = "MSc", ["Institution"] = "University", ["Period"] = "2020-2024", ["Description"] = "Source description" },
            _ => new() { ["Name"] = "Cloud", ["Issuer"] = "Institute", ["Period"] = "2021-2024", ["Identifier"] = "ABC-123" }
        };
        var text = string.Join(" | ", fields.Values);
        var resume = ResumeDocument.Create(documents.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, text)])), DateTime.UtcNow);
        var fact = CandidateFactBuilder.Create(extraction, new(kind, fields.Select(x =>
            new EvidenceSelection(x.Key, 1, text.IndexOf(x.Value, StringComparison.Ordinal), x.Value.Length)).ToArray()), DateTime.UtcNow);
        fact.Review(documents.OwnerId, "Approved", null, DateTime.UtcNow);
        using (var scope = documents.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            db.Resumes.Add(resume); db.Extractions.Add(extraction); db.CandidateFacts.Add(fact); await db.SaveChangesAsync();
        }
        if (confirm)
        {
            using var response = await client.PostAsJsonAsync(ImportPath(fact).Replace("profile-import", "profile-mapping"), DatedCommand(kind));
            response.EnsureSuccessStatusCode();
        }
        return fact;
    }
    private Guid AddExisting(ProfileDb db, string kind, bool conflicting = false)
    {
        var end = conflicting ? new DateOnly(2024, 8, 1) : new DateOnly(2024, 7, 19);
        if (kind == "Experience")
        {
            var entity = new JobApplicationAgent.Profile.Domain.Entities.ProfessionalExperience(profile.Profile.Id, "acme", "engineer",
                new(2020, 3, 12), end, false, "Paris", "Verified description");
            db.ProfessionalExperiences.Add(entity); return entity.Id;
        }
        if (kind == "Education")
        {
            var entity = new JobApplicationAgent.Profile.Domain.Entities.Education(profile.Profile.Id, "university", "msc",
                new(2020, 3, 12), end, false, "Verified specialty", "Paris", "Verified description");
            db.Educations.Add(entity); return entity.Id;
        }
        var certificate = new JobApplicationAgent.Profile.Domain.Entities.Certification(profile.Profile.Id, "cloud", "institute",
            new(2021, 3, 12), end, "ABC-123", "https://example.test/verified");
        db.Certifications.Add(certificate); return certificate.Id;
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public async Task DatedFact_ShouldRecoverLostReceiptAndNeverResurrectDeletedTarget(string kind)
    {
        var fact = await SeedDated(kind); loseNextPost = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(ImportPath(fact), null)).StatusCode);
        var receipt = await Import(fact);
        Assert.Equal(kind, receipt.Kind); Assert.NotEqual(Guid.Empty, receipt.EntityId); Assert.Equal(Guid.Empty, receipt.SkillId);
        Assert.Equal("Created", receipt.Action); Assert.Equal(1, posts);
        using var scope = profile.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        var audit = Assert.Single(await db.DocumentFactImports.ToArrayAsync());
        Assert.Equal(kind, audit.Kind); Assert.Equal(receipt.EntityId, audit.EntityId);
        Assert.Contains("ConfirmedByOwnerId", audit.RequestJson); Assert.Contains("ConfirmationNote", audit.RequestJson);
        if (kind == "Experience")
        {
            var entity = Assert.Single(await db.ProfessionalExperiences.ToArrayAsync());
            Assert.Equal(receipt.EntityId, entity.Id); Assert.Equal("Engineer", entity.JobTitle); Assert.Equal("Acme", entity.CompanyName);
            Assert.Equal(new DateOnly(2020, 3, 12), entity.StartDate); Assert.Equal(new DateOnly(2024, 7, 19), entity.EndDate);
            Assert.Equal("Source description", entity.Description); Assert.Null(entity.Location);
            await db.ProfessionalExperiences.ExecuteDeleteAsync();
        }
        else if (kind == "Education")
        {
            var entity = Assert.Single(await db.Educations.ToArrayAsync());
            Assert.Equal(receipt.EntityId, entity.Id); Assert.Equal("MSc", entity.Degree); Assert.Equal("University", entity.InstitutionName);
            Assert.Equal(new DateOnly(2020, 3, 12), entity.StartDate); Assert.Null(entity.FieldOfStudy);
            await db.Educations.ExecuteDeleteAsync();
        }
        else
        {
            var entity = Assert.Single(await db.Certifications.ToArrayAsync());
            Assert.Equal(receipt.EntityId, entity.Id); Assert.Equal("ABC-123", entity.CredentialId); Assert.Equal("Cloud", entity.Name);
            Assert.Equal(new DateOnly(2021, 3, 12), entity.IssueDate); Assert.Null(entity.CredentialUrl);
            await db.Certifications.ExecuteDeleteAsync();
        }
        Assert.Equal(receipt, await Import(fact)); Assert.Equal(1, posts);
        Assert.False(await db.ProfessionalExperiences.AnyAsync()); Assert.False(await db.Educations.AnyAsync()); Assert.False(await db.Certifications.AnyAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public async Task ConcurrentDatedFacts_ShouldLinkExistingWithoutOverwritingVerifiedData(string kind)
    {
        Guid id;
        using (var scope = profile.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<ProfileDb>(); id = AddExisting(db, kind); await db.SaveChangesAsync(); }
        var first = await SeedDated(kind); var second = await SeedDated(kind);
        var receipts = await Task.WhenAll(Import(first), Import(second), Import(first));
        Assert.All(receipts, x => { Assert.Equal(id, x.EntityId); Assert.Equal("LinkedExisting", x.Action); });
        using var check = profile.Services.CreateScope(); var context = check.ServiceProvider.GetRequiredService<ProfileDb>();
        if (kind == "Experience")
        { var entity = Assert.Single(await context.ProfessionalExperiences.ToArrayAsync()); Assert.Equal("Verified description", entity.Description); Assert.Equal("Paris", entity.Location); }
        else if (kind == "Education")
        { var entity = Assert.Single(await context.Educations.ToArrayAsync()); Assert.Equal("Verified specialty", entity.FieldOfStudy); Assert.Equal("Verified description", entity.Description); }
        else Assert.Equal("https://example.test/verified", Assert.Single(await context.Certifications.ToArrayAsync()).CredentialUrl);
        Assert.Equal(2, await context.DocumentFactImports.CountAsync());
    }
    [Theory]
    [InlineData("Experience", false)]
    [InlineData("Education", false)]
    [InlineData("Certification", false)]
    [InlineData("Experience", true)]
    [InlineData("Education", true)]
    [InlineData("Certification", true)]
    public async Task ConflictingOrAmbiguousExistingRecords_ShouldRejectWithoutReceipt(string kind, bool ambiguous)
    {
        var fact = await SeedDated(kind);
        using var scope = profile.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        AddExisting(db, kind, !ambiguous); if (ambiguous) AddExisting(db, kind);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync(ImportPath(fact), null)).StatusCode);
        Assert.Empty(await db.DocumentFactImports.ToArrayAsync());
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public async Task DatedFactWithoutConfirmedMapping_ShouldNeverCallProfile(string kind)
    {
        var fact = await SeedDated(kind, false);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync(ImportPath(fact), null)).StatusCode);
        Assert.Equal(0, posts);
    }
    [Theory]
    [InlineData("Experience")]
    [InlineData("Education")]
    [InlineData("Certification")]
    public async Task ConcurrentNewDatedFacts_ShouldCreateOneTargetAndTwoReceipts(string kind)
    {
        var first = await SeedDated(kind); var second = await SeedDated(kind);
        var receipts = await Task.WhenAll(Import(first), Import(second));
        Assert.Single(receipts.Select(x => x.EntityId).Distinct());
        Assert.Single(receipts, x => x.Action == "Created"); Assert.Single(receipts, x => x.Action == "LinkedExisting");
        using var scope = profile.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        Assert.Equal(2, await db.DocumentFactImports.CountAsync());
        Assert.Equal(1, await db.ProfessionalExperiences.CountAsync() + await db.Educations.CountAsync() + await db.Certifications.CountAsync());
    }
    [Fact]
    public async Task LegacySkillReceipt_ShouldSurviveSchemaMigrationAndAnIdenticalDirectRetry()
    {
        var fact = await Seed(); var fields = CandidateFactDto.From(fact).Fields;
        var payload = new { FactId = fact.Id, fact.OwnerId, TargetProfileId = profile.Profile.Id, fact.ResumeId, fact.ExtractionId,
            fact.SourceSha256, fact.EngineVersion, fact.Kind, fact.Decision, fact.ReviewedAtUtc, fact.ReviewedByOwnerId, Fields = fields };
        var json = JsonSerializer.Serialize(payload);
        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        var skill = new Skill(profile.Profile.Id, "SQL");
        using var scope = profile.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        db.Skills.Add(skill); await db.SaveChangesAsync();
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        await migrator.MigrateAsync("20261015135115_AddDocumentFactImports");
        var at = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO document_fact_imports (\"FactId\", \"OwnerId\", \"ProfileId\", \"SkillId\", \"SourceSha256\", \"Action\", \"RequestHash\", \"RequestJson\", \"ImportedAtUtc\") VALUES ({fact.Id}, {fact.OwnerId}, {profile.Profile.Id}, {skill.Id}, {fact.SourceSha256}, {"Created"}, {hash}, CAST({json} AS jsonb), {at})");
        await migrator.MigrateAsync();
        using var receiver = profile.CreateClient(); receiver.DefaultRequestHeaders.Authorization = new("Bearer", profile.Token);
        using var retry = await receiver.PostAsJsonAsync("/api/v1/profile/document-facts", payload);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var receipt = (await retry.Content.ReadFromJsonAsync<ProfileFactReceipt>())!;
        Assert.Equal("Skill", receipt.Kind); Assert.Equal(skill.Id, receipt.EntityId); Assert.Equal(skill.Id, receipt.SkillId);
        Assert.Equal(hash, (await db.DocumentFactImports.AsNoTracking().SingleAsync()).RequestHash);
        Assert.Single(await db.Skills.AsNoTracking().ToArrayAsync());
    }
    [Fact]
    public async Task ApprovedSkill_ShouldCreateAuditedReceiptAndNeverResurrectDeletedSkillOnRetry()
    {
        var fact = await Seed();
        var receipt = await Import(fact);
        Assert.Equal("Created", receipt.Action);
        Assert.Equal(profile.Profile.Id, receipt.ProfileId);
        Assert.Equal(receipt, await Import(fact));
        using var scope = profile.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        Assert.False(db.Database.HasPendingModelChanges());
        var skill = Assert.Single(await db.Skills.ToArrayAsync());
        Assert.Equal("SQL", skill.Name);
        Assert.Null(skill.Level); Assert.Null(skill.Category); Assert.Null(skill.YearsOfExperience);
        var audit = Assert.Single(await db.DocumentFactImports.ToArrayAsync());
        Assert.Contains(fact.ExtractionId.ToString(), audit.RequestJson);
        await db.Skills.ExecuteDeleteAsync();
        Assert.Equal(receipt, await Import(fact));
        Assert.Empty(await db.Skills.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, posts);
    }
    [Fact]
    public async Task SeparateConcurrentFacts_ShouldLinkExistingSkillWithoutOverwritingVerifiedMetadata()
    {
        Guid existingId;
        using (var scope = profile.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
            var existing = new Skill(profile.Profile.Id, "sql", "Database", "Expert", 9);
            existingId = existing.Id; db.Skills.Add(existing); await db.SaveChangesAsync();
        }
        var first = await Seed(); var second = await Seed(value: " SQL ");
        var receipts = await Task.WhenAll(Import(first), Import(second), Import(first));
        Assert.All(receipts, x => { Assert.Equal(existingId, x.SkillId); Assert.Equal("LinkedExisting", x.Action); });
        using var check = profile.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<ProfileDb>();
        var skill = Assert.Single(await context.Skills.ToArrayAsync());
        Assert.Equal("sql", skill.Name); Assert.Equal("Expert", skill.Level); Assert.Equal(9, skill.YearsOfExperience);
        Assert.Equal("Database", skill.Category);
        Assert.Equal(2, await context.DocumentFactImports.CountAsync());
    }
    [Fact]
    public async Task LostResponseAfterCommit_ShouldRecoverReceiptWithoutRepeatingPost()
    {
        var fact = await Seed(); loseNextPost = true;
        using var failed = await client.PostAsync(ImportPath(fact), null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var recovered = await Import(fact);
        Assert.Equal("Created", recovered.Action); Assert.Equal(1, posts);
        using var scope = profile.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        Assert.Single(await db.Skills.ToArrayAsync()); Assert.Single(await db.DocumentFactImports.ToArrayAsync());
    }
    [Fact]
    public async Task UnapprovedUnsupportedAndForeignFacts_ShouldNeverReachProfile()
    {
        foreach (var fact in new[] { await Seed(decision: null), await Seed(decision: "Rejected"),
            await Seed(kind: "Experience"), await Seed(value: new string('a', 151)) })
        {
            using var rejected = await client.PostAsync(ImportPath(fact), null);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        }
        var foreign = await Seed(owner: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(ImportPath(foreign), null)).StatusCode);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(ImportPath(foreign), null)).StatusCode);
        Assert.Equal(0, posts);
    }
    [Fact]
    public async Task Receiver_ShouldRejectWrongCredentialsBindingAndChangedPayload()
    {
        var fact = await Seed(); var receipt = await Import(fact);
        using var receiver = profile.CreateClient();
        var route = $"/api/v1/profile/document-facts/{fact.Id}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await receiver.GetAsync(route)).StatusCode);
        receiver.DefaultRequestHeaders.Authorization = new("Bearer", documents.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await receiver.GetAsync(route)).StatusCode);
        receiver.DefaultRequestHeaders.Authorization = new("Bearer", profile.Token);
        Assert.Equal(receipt, await receiver.GetFromJsonAsync<ProfileFactReceipt>(route));
        object Payload(Guid owner, string value, string decision = "Approved") => new
        {
            FactId = fact.Id, OwnerId = owner, TargetProfileId = profile.Profile.Id, fact.ResumeId, fact.ExtractionId,
            fact.SourceSha256, fact.EngineVersion, fact.Kind, Decision = decision, fact.ReviewedAtUtc,
            ReviewedByOwnerId = owner, Fields = new[] { new CandidateFactField("Name", value, 1, 0, value.Length, value) }
        };
        Assert.Equal(HttpStatusCode.Conflict, (await receiver.PostAsJsonAsync("/api/v1/profile/document-facts", Payload(documents.OwnerId, "Azure"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await receiver.PostAsJsonAsync("/api/v1/profile/document-facts", Payload(Guid.NewGuid(), "SQL"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await receiver.PostAsJsonAsync("/api/v1/profile/document-facts", Payload(documents.OwnerId, "SQL", "Pending"))).StatusCode);
    }
    [Theory]
    [InlineData("token")]
    [InlineData("target")]
    [InlineData("remote-http")]
    public async Task InvalidSenderConfiguration_ShouldFailWithoutSendingAPost(string invalid)
    {
        var fact = await Seed();
        var settings = invalid switch
        {
            "token" => new Dictionary<string, string?> { ["Documents:ProfileImport:Token"] = "" },
            "target" => new() { ["Documents:ProfileImport:ProfileId"] = "invalid" },
            _ => new() { ["Documents:ProfileImport:BaseUrl"] = "http://external.example.test" }
        };
        using var invalidHost = host.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)));
        using var invalidClient = invalidHost.CreateClient();
        invalidClient.DefaultRequestHeaders.Authorization = new("Bearer", documents.Token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await invalidClient.PostAsync(ImportPath(fact), null)).StatusCode);
        Assert.Equal(0, posts);
    }
    [Fact]
    public async Task ReceiverWithoutBinding_ShouldStayDisabled()
    {
        using var disabled = profile.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Profile:DocumentImport:OwnerId"] = "" })));
        using var receiver = disabled.CreateClient();
        receiver.DefaultRequestHeaders.Authorization = new("Bearer", profile.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await receiver.GetAsync($"/api/v1/profile/document-facts/{Guid.NewGuid()}")).StatusCode);
    }
    [Fact]
    public async Task MissingTargetAndAmbiguousSkills_ShouldNeverCreateAnImportReceipt()
    {
        var fact = await Seed();
        using var scope = profile.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProfileDb>();
        db.Skills.AddRange(new Skill(profile.Profile.Id, "SQL"), new Skill(profile.Profile.Id, " sql "));
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync(ImportPath(fact), null)).StatusCode);
        Assert.Equal(0, await db.DocumentFactImports.CountAsync());
        await db.CandidateProfiles.ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(ImportPath(fact), null)).StatusCode);
        Assert.Equal(0, await db.DocumentFactImports.CountAsync());
    }
    private sealed class LostResponseHandler(HttpMessageHandler inner, Func<bool> lose) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (request.Method == HttpMethod.Post && lose())
            { response.Dispose(); throw new HttpRequestException("Simulated lost response after commit"); }
            return response;
        }
    }
}
