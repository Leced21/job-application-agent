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

public sealed class CandidateFactApiTests : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private const string Text = "SQL Engineer Acme. Not certified in Azure. MSc University.";
    private readonly DocumentsApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private readonly HttpClient client;
    public CandidateFactApiTests(DocumentsApiFactory factory)
    {
        this.factory = factory;
        host = factory.WithWebHostBuilder(_ => { });
        client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private static string Route(Guid id) => $"/api/v1/documents/resumes/{id}/facts";
    private static EvidenceSelection Select(string field, string value) => new(field, 1, Text.IndexOf(value, StringComparison.Ordinal), value.Length);
    private async Task<ResumeTextExtraction> Seed(Guid? owner = null, string text = Text)
    {
        var document = ResumeDocument.Create(owner ?? factory.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(document, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, text)])), DateTime.UtcNow);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(document);
        db.Extractions.Add(extraction);
        await db.SaveChangesAsync();
        return extraction;
    }
    private async Task<CandidateFactDto> Create(Guid resumeId, CreateCandidateFactCommand command)
    {
        using var response = await client.PostAsJsonAsync(Route(resumeId), command);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CandidateFactDto>())!;
    }

    [Fact]
    public async Task StructuredEvidenceAndReview_ShouldPreserveSourceAndExposeOnlyApprovedSubset()
    {
        var source = await Seed();
        var schemas = (await client.GetFromJsonAsync<CandidateFactSchema[]>("/api/v1/documents/fact-schema"))!;
        Assert.Equal(4, schemas.Length);
        var command = new CreateCandidateFactCommand("Experience", [Select("Title", "Engineer"), Select("Company", "Acme")]);
        using var created = await client.PostAsJsonAsync(Route(source.ResumeId), command);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fact = (await created.Content.ReadFromJsonAsync<CandidateFactDto>())!;
        Assert.Equal("Pending", fact.Decision);
        Assert.Equal(source.Id, fact.ExtractionId);
        Assert.Equal(source.SourceSha256, fact.SourceSha256);
        Assert.Equal("Engineer", fact.Fields.Single(x => x.Field == "Title").Value);
        Assert.Empty((await client.GetFromJsonAsync<CandidateFactDto[]>(Route(source.ResumeId) + "?decision=Approved"))!);
        var reviewPath = $"{Route(source.ResumeId)}/{fact.Id}/review";
        using var approved = await client.PostAsJsonAsync(reviewPath, new ReviewCandidateFactCommand("Approved", "Confirmed against original CV"));
        approved.EnsureSuccessStatusCode();
        var reviewed = (await approved.Content.ReadFromJsonAsync<CandidateFactDto>())!;
        Assert.NotNull(reviewed.ReviewedAtUtc);
        Assert.Equal(factory.OwnerId, reviewed.ReviewedByOwnerId);
        using var retried = await client.PostAsJsonAsync(reviewPath, new ReviewCandidateFactCommand("Approved", reviewed.ReviewNote));
        Assert.Equal(reviewed.ReviewedAtUtc, (await retried.Content.ReadFromJsonAsync<CandidateFactDto>())!.ReviewedAtUtc);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(reviewPath, new ReviewCandidateFactCommand("Rejected"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(reviewPath, new ReviewCandidateFactCommand("Approved", "changed"))).StatusCode);
        var duplicate = await Create(source.ResumeId, command with { Fields = command.Fields!.Reverse().ToArray() });
        Assert.Equal(fact.Id, duplicate.Id);
        Assert.Equal("Approved", duplicate.Decision);
        var negated = await Create(source.ResumeId, new("Certification", [Select("Name", "Azure")]));
        Assert.Equal("Pending", negated.Decision);
        Assert.Contains("Not certified in Azure", Assert.Single(negated.Fields).Excerpt);
        (await client.PostAsJsonAsync($"{Route(source.ResumeId)}/{negated.Id}/review",
            new ReviewCandidateFactCommand("Rejected", "The source explicitly negates this qualification"))).EnsureSuccessStatusCode();
        await Create(source.ResumeId, new("Skill", [Select("Name", "SQL")]));
        var approvedSubset = (await client.GetFromJsonAsync<CandidateFactDto[]>(Route(source.ResumeId) + "?decision=Approved"))!;
        Assert.Equal(fact.Id, Assert.Single(approvedSubset).Id);
        Assert.Single((await client.GetFromJsonAsync<CandidateFactDto[]>(Route(source.ResumeId) + "?page=2&pageSize=1"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route(source.ResumeId) + "?decision=unknown")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task ConcurrentAnnotationAndOppositeReviews_ShouldKeepOneFactAndOneDecision()
    {
        var source = await Seed();
        var command = new CreateCandidateFactCommand("Skill", [Select("Name", "SQL")]);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route(source.ResumeId), command)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var facts = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<CandidateFactDto>()));
        var factId = Assert.Single(facts.Select(x => x!.Id).Distinct());
        foreach (var response in responses) response.Dispose();
        var reviews = await Task.WhenAll(client.PostAsJsonAsync($"{Route(source.ResumeId)}/{factId}/review", new ReviewCandidateFactCommand("Approved")),
            client.PostAsJsonAsync($"{Route(source.ResumeId)}/{factId}/review", new ReviewCandidateFactCommand("Rejected")));
        Assert.Single(reviews, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(reviews, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var review in reviews) review.Dispose();
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().CandidateFacts.CountAsync());
    }

    [Theory]
    [InlineData("range")]
    [InlineData("missing-field")]
    [InlineData("invented-field")]
    public async Task InvalidEvidence_ShouldNeverPersist(string invalid)
    {
        var source = await Seed();
        var command = invalid switch
        {
            "range" => new CreateCandidateFactCommand("Skill", [new("Name", 1, 10000, 3)]),
            "missing-field" => new("Experience", [Select("Company", "Acme")]),
            _ => new("Skill", [Select("YearsExperience", "SQL")])
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route(source.ResumeId), command)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().CandidateFacts.CountAsync());
    }

    [Fact]
    public async Task OwnerAndResumeScoping_ShouldProtectEveryFactOperation()
    {
        var source = await Seed();
        var fact = await Create(source.ResumeId, new("Skill", [Select("Name", "SQL")]));
        var otherResume = await Seed();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route(otherResume.ResumeId)}/{fact.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Route(otherResume.ResumeId)}/{fact.Id}/review", new ReviewCandidateFactCommand("Approved"))).StatusCode);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"{Route(source.ResumeId)}/{fact.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route(source.ResumeId), new CreateCandidateFactCommand("Skill", [Select("Name", "SQL")]))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            var ownerB = Guid.NewGuid();
            await db.CandidateFacts.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OwnerId, ownerB));
            await db.Extractions.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OwnerId, ownerB));
            await db.Resumes.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OwnerId, ownerB));
        }
        client.DefaultRequestHeaders.Add("X-Owner-Id", source.OwnerId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route(source.ResumeId)}/{fact.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(source.ResumeId) + "?decision=Approved")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route(source.ResumeId), new CreateCandidateFactCommand("Skill", [Select("Name", "SQL")]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Route(source.ResumeId)}/{fact.Id}/review", new ReviewCandidateFactCommand("Approved"))).StatusCode);
    }

    [Fact]
    public async Task FactLimit_ShouldRejectNewAnnotationsButAllowAnExistingRetry()
    {
        var text = string.Join(" ", Enumerable.Range(0, 200).Select(i => $"Skill{i:D3}"));
        var source = await Seed(text: text);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            db.CandidateFacts.AddRange(Enumerable.Range(0, 200).Select(i => CandidateFactBuilder.Create(source,
                new("Skill", [new("Name", 1, i * 9, 8)]), DateTime.UtcNow)));
            await db.SaveChangesAsync();
        }
        var existing = new CreateCandidateFactCommand("Skill", [new("Name", 1, 0, 8)]);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Route(source.ResumeId), existing)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route(source.ResumeId), existing with { Kind = "Certification" })).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal(200, await finalScope.ServiceProvider.GetRequiredService<DocumentsDbContext>().CandidateFacts.CountAsync());
    }
}
