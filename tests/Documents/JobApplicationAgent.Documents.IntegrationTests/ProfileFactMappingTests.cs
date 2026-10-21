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

public sealed class ProfileFactMappingTests : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private readonly DocumentsApiFactory factory;
    private readonly WebApplicationFactory<Program> host;
    private readonly HttpClient client;
    public ProfileFactMappingTests(DocumentsApiFactory factory)
    {
        this.factory = factory;
        host = factory.WithWebHostBuilder(_ => { }); client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token);
    }
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { client.Dispose(); await host.DisposeAsync(); await factory.ResetAsync(); }
    private static string Route(ResumeCandidateFact fact) => $"/api/v1/documents/resumes/{fact.ResumeId}/facts/{fact.Id}/profile-mapping";
    private static ConfirmProfileDatesCommand Dates(string kind = "Experience") => kind == "Certification"
        ? new(null, null, null, new(2021, 3, 12), null, true, "Exact issue date checked in the original certificate.")
        : new(new(2020, 3, 12), new(2024, 7, 19), false, null, null, true, "Exact dates checked against my employment or education records.");
    private async Task<ResumeCandidateFact> Seed(string kind = "Experience", string? decision = "Approved", string? omit = null, Guid? owner = null)
    {
        var fields = kind switch
        {
            "Experience" => new Dictionary<string, string> { ["Title"] = "Engineer", ["Company"] = "Acme", ["Period"] = "2020-2024", ["Description"] = "Built systems" },
            "Education" => new() { ["Degree"] = "MSc", ["Institution"] = "University", ["Period"] = "2020-2024" },
            "Certification" => new() { ["Name"] = "Cloud", ["Issuer"] = "Institute", ["Period"] = "2021", ["Identifier"] = "ABC-123" },
            _ => new() { ["Name"] = "SQL" }
        };
        if (omit is not null) fields.Remove(omit);
        var text = string.Join(" | ", fields.Values);
        var resume = ResumeDocument.Create(owner ?? factory.OwnerId, "cv.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        var extraction = ResumeTextExtraction.Create(resume, ExtractedText.Version,
            JsonSerializer.Serialize(new ExtractedText(ExtractedText.Version, "Page", [new(1, text)])), DateTime.UtcNow);
        var fact = CandidateFactBuilder.Create(extraction, new(kind, fields.Select(x =>
            new EvidenceSelection(x.Key, 1, text.IndexOf(x.Value, StringComparison.Ordinal), x.Value.Length)).ToArray()), DateTime.UtcNow);
        if (decision is not null) fact.Review(resume.OwnerId, decision, null, DateTime.UtcNow);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        db.Resumes.Add(resume); db.Extractions.Add(extraction); db.CandidateFacts.Add(fact); await db.SaveChangesAsync();
        return fact;
    }
    [Theory]
    [InlineData("Experience", "JobTitle", "Engineer")]
    [InlineData("Education", "Degree", "MSc")]
    [InlineData("Certification", "Name", "Cloud")]
    public async Task ConfirmedMapping_ShouldPreserveSourceAndRoundTripWithoutImporting(string kind, string label, string value)
    {
        var fact = await Seed(kind); var command = Dates(kind);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(fact))).StatusCode);
        using var confirmed = await client.PostAsJsonAsync(Route(fact), command); confirmed.EnsureSuccessStatusCode();
        var mapping = (await confirmed.Content.ReadFromJsonAsync<ProfileFactMappingDto>())!;
        Assert.Equal("OwnerConfirmed", mapping.DatesOrigin); Assert.Equal(factory.OwnerId, mapping.ConfirmedByOwnerId);
        Assert.Equal(fact.SourceSha256, mapping.SourceSha256); Assert.Equal(value, ((JsonElement)mapping.TargetFields[label]!).GetString());
        Assert.Equal(command, mapping.Dates);
        Assert.Equal(kind == "Certification" ? "2021" : "2020-2024", mapping.Evidence.Single(x => x.Field == "Period").Value);
        using var retry = await client.PostAsJsonAsync(Route(fact), command); retry.EnsureSuccessStatusCode();
        Assert.Equal(mapping.ConfirmedAtUtc, (await retry.Content.ReadFromJsonAsync<ProfileFactMappingDto>())!.ConfirmedAtUtc);
        var read = (await client.GetFromJsonAsync<ProfileFactMappingDto>(Route(fact)))!;
        Assert.Equal(JsonSerializer.Serialize(mapping), JsonSerializer.Serialize(read));
        // This standalone host has no Profile import credentials: confirmation never imports implicitly.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(Route(fact).Replace("profile-mapping", "profile-import"), null)).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var saved = await db.CandidateFacts.SingleAsync(x => x.Id == fact.Id);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(fact.FieldsJson),
            System.Text.Json.Nodes.JsonNode.Parse(saved.FieldsJson)));
        Assert.Equal(fact.ReviewedAtUtc, saved.ReviewedAtUtc);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Theory]
    [InlineData("confirmation")]
    [InlineData("note")]
    [InlineData("long-note")]
    [InlineData("start")]
    [InlineData("minimum")]
    [InlineData("end")]
    [InlineData("order")]
    [InlineData("current")]
    [InlineData("current-with-end")]
    [InlineData("issue-date")]
    public async Task InvalidDates_ShouldLeaveMappingAbsent(string invalid)
    {
        var fact = await Seed(); var command = Dates();
        command = invalid switch
        {
            "confirmation" => command with { Confirmed = false }, "note" => command with { ConfirmationNote = " " },
            "long-note" => command with { ConfirmationNote = new string('a', 1001) },
            "start" => command with { StartDate = null }, "minimum" => command with { StartDate = DateOnly.MinValue },
            "end" => command with { EndDate = null }, "order" => command with { EndDate = new(2019, 1, 1) },
            "current" => command with { IsCurrent = null }, "current-with-end" => command with { IsCurrent = true },
            _ => command with { IssueDate = new(2020, 1, 1) }
        };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route(fact), command)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(fact))).StatusCode);
    }
    [Theory]
    [InlineData("Experience", "Company")]
    [InlineData("Education", "Institution")]
    [InlineData("Certification", "Issuer")]
    [InlineData("Experience", "Period")]
    public async Task MissingSourceFields_ShouldNeverBeInvented(string kind, string omit)
    {
        var fact = await Seed(kind, omit: omit);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route(fact), Dates(kind))).StatusCode);
    }
    [Theory]
    [InlineData("issue")]
    [InlineData("expiration")]
    [InlineData("employment")]
    public async Task InvalidCertificationDates_ShouldBeRejected(string invalid)
    {
        var fact = await Seed("Certification"); var command = Dates("Certification");
        command = invalid switch
        {
            "issue" => command with { IssueDate = null },
            "expiration" => command with { ExpirationDate = new(2020, 1, 1) },
            _ => command with { IsCurrent = false }
        };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route(fact), command)).StatusCode);
    }
    [Fact]
    public async Task CurrentPeriod_ShouldRequireNoEndAndKeepItUnknown()
    {
        var fact = await Seed(); var command = Dates() with { EndDate = null, IsCurrent = true };
        using var response = await client.PostAsJsonAsync(Route(fact), command); response.EnsureSuccessStatusCode();
        var mapping = (await response.Content.ReadFromJsonAsync<ProfileFactMappingDto>())!;
        Assert.Null(mapping.TargetFields["EndDate"]);
    }
    [Fact]
    public async Task UnicodeConfirmationNoteAtLimit_ShouldSurviveJsonEncodingAndRetry()
    {
        var fact = await Seed(); var command = Dates() with { ConfirmationNote = new string('é', 1000) };
        using var response = await client.PostAsJsonAsync(Route(fact), command); response.EnsureSuccessStatusCode();
        using var retry = await client.PostAsJsonAsync(Route(fact), command); retry.EnsureSuccessStatusCode();
        Assert.Equal(command.ConfirmationNote, (await retry.Content.ReadFromJsonAsync<ProfileFactMappingDto>())!.Dates.ConfirmationNote);
    }
    [Theory]
    [InlineData("2020")]
    [InlineData("2020-03")]
    public async Task PartialDateOnWire_ShouldNotBeExpandedToAnInventedDay(string partial)
    {
        var fact = await Seed();
        using var content = new StringContent(JsonSerializer.Serialize(new
        {
            startDate = partial, endDate = "2024-07-19", isCurrent = false, confirmed = true, confirmationNote = "Verified"
        }), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Route(fact), content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(fact))).StatusCode);
    }
    [Fact]
    public async Task ConcurrentDifferentConfirmations_ShouldKeepExactlyOneFinalMapping()
    {
        var fact = await Seed(); var first = Dates(); var second = first with { EndDate = new(2024, 8, 1) };
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Route(fact), first), client.PostAsJsonAsync(Route(fact), second));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var mapping = (await client.GetFromJsonAsync<ProfileFactMappingDto>(Route(fact)))!;
        using var retry = await client.PostAsJsonAsync(Route(fact), mapping.Dates); retry.EnsureSuccessStatusCode();
        Assert.Equal(mapping.ConfirmedAtUtc, (await retry.Content.ReadFromJsonAsync<ProfileFactMappingDto>())!.ConfirmedAtUtc);
        foreach (var response in responses) response.Dispose();
    }
    [Fact]
    public async Task OwnershipApprovalAndResume_ShouldProtectMappingOperations()
    {
        foreach (var fact in new[] { await Seed(decision: null), await Seed(decision: "Rejected"), await Seed("Skill") })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Route(fact), Dates())).StatusCode);
        var foreign = await Seed(owner: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Route(foreign), Dates())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Route(foreign))).StatusCode);
        var own = await Seed();
        var wrongResume = Route(own).Replace(own.ResumeId.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(wrongResume, Dates())).StatusCode);
        using var anonymous = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route(own), Dates())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route(own))).StatusCode);
    }
}
