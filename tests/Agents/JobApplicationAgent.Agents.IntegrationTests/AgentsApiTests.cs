using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class AgentsApiTests(AgentsFactory fixture) : IClassFixture<AgentsFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static Task<JsonElement> Body(HttpResponseMessage response) => response.Content.ReadFromJsonAsync<JsonElement>();
    private static async Task<JsonElement> Run(HttpClient client, AgentCommand command)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/agents/runs", command, Json);
        response.EnsureSuccessStatusCode(); return await Body(response);
    }
    [Theory] [InlineData("/catalog")] [InlineData("/budget")] [InlineData("/runs/00000000-0000-0000-0000-000000000001")]
    public async Task ReadRequiresAuthentication(string path)
    {
        using var client = fixture.Client(authorized: false);
        using var response = await client.GetAsync("/api/v1/agents" + path); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }
    [Fact] public async Task BadTokenCannotStartRun()
    {
        using var client = fixture.Client(); client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 64));
        using var response = await client.PostAsJsonAsync("/api/v1/agents/runs", new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    [Fact] public async Task CatalogReportsDisabledProductionProvider()
    {
        using var client = fixture.Client(simulated: false); var result = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/catalog");
        Assert.False(result.GetProperty("modelAvailable").GetBoolean()); Assert.Equal(7, result.GetProperty("definitions").GetArrayLength());
    }
    [Fact] public async Task RealJobSourceIsFrozenAndReplaySurvivesItsDeletion()
    {
        var owner = Guid.NewGuid(); using var client = fixture.Client(owner: owner);
        var id = await fixture.NewJob(); var command = new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, id);
        var run = await Run(client, command); Assert.Equal("PendingReview", run.GetProperty("state").GetString());
        Assert.Equal("SQL required. Rust optional.", run.GetProperty("proposal").GetProperty("source").GetProperty("text").GetString());
        using (var deleted = await fixture.Jobs.DeleteAsync($"/api/v1/jobs/{id}")) deleted.EnsureSuccessStatusCode();
        using var restarted = fixture.Client(owner: owner);
        var replay = await Run(restarted, command); Assert.Equal(run.ToString(), replay.ToString()); Assert.False(fixture.SawCredentials);
        var budget = await restarted.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget"); Assert.Equal(140, budget.GetProperty("chargedTokens").GetInt64());
        Assert.Equal(0, budget.GetProperty("heldTokens").GetInt64());
    }
    [Fact] public async Task ReviewRequiresExactHashAndCannotBeRepeated()
    {
        using var client = fixture.Client(owner: Guid.NewGuid());
        var run = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob()));
        var id = run.GetProperty("id").GetGuid(); var hash = run.GetProperty("proposal").GetProperty("outputSha256").GetString()!;
        using var wrong = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, "wrong", true, true));
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        using var approved = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, hash, true, true, "Verified"));
        approved.EnsureSuccessStatusCode(); Assert.Equal("Approved", (await Body(approved)).GetProperty("state").GetString());
        using var again = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, hash, true, false));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/runs/{id}"); Assert.Equal(3, saved.GetProperty("revision").GetInt64());
        var audits = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/runs/{id}/audit"); Assert.Equal(3, audits.GetArrayLength());
        Assert.DoesNotContain("SQL", audits.ToString()); Assert.DoesNotContain("Verified", audits.ToString());
    }
    [Fact] public async Task OwnerIsolationAppliesToReadsReviewsAndRecovery()
    {
        using var client = fixture.Client(owner: Guid.NewGuid());
        var run = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob()));
        var id = run.GetProperty("id").GetGuid(); using var other = fixture.Client(owner: Guid.NewGuid());
        foreach (var path in new[] { $"/runs/{id}", $"/runs/{id}/audit" })
        { using var response = await other.GetAsync("/api/v1/agents" + path); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        using var review = await other.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, "wrong", true, true));
        Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
        using var recovery = await other.PostAsync($"/api/v1/agents/runs/{id}/recover", null); Assert.Equal(HttpStatusCode.NotFound, recovery.StatusCode);
    }
    [Fact] public async Task DisabledProviderFailsDurablyWithoutAnyCharge()
    {
        using var client = fixture.Client(simulated: false, owner: Guid.NewGuid());
        var result = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob()));
        Assert.Equal("Failed", result.GetProperty("state").GetString()); Assert.Equal("ModelUnavailable", result.GetProperty("failure").GetString());
        var budget = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget"); Assert.Equal(0, budget.GetProperty("chargedTokens").GetInt64());
    }
    [Fact] public async Task MissingJobIsRejectedBeforeModelAndQuota()
    {
        using var client = fixture.Client(owner: Guid.NewGuid());
        var run = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()));
        Assert.Equal("SourceRejected", run.GetProperty("failure").GetString());
    }
    [Fact] public async Task RequestCannotBeReusedForAnotherJob()
    {
        using var client = fixture.Client(owner: Guid.NewGuid()); var request = Guid.NewGuid();
        await Run(client, new(request, AgentKind.JobAnalysis, await fixture.NewJob()));
        using var conflict = await client.PostAsJsonAsync("/api/v1/agents/runs", new AgentCommand(request, AgentKind.JobAnalysis, await fixture.NewJob()), Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
    [Fact] public async Task InvalidIdentityIsBadRequestAndErrorsAreSanitized()
    {
        using var client = fixture.Client();
        using var invalid = await client.PostAsJsonAsync("/api/v1/agents/runs", new AgentCommand(Guid.Empty, AgentKind.JobAnalysis, Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); var text = await invalid.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", text); Assert.DoesNotContain(fixture.Token, text);
        Assert.Equal("no-store", invalid.Headers.CacheControl!.ToString());
    }
    [Fact] public async Task WriteRateLimitIsEnforced()
    {
        using var client = fixture.Client(owner: Guid.NewGuid());
        for (var i = 0; i < 10; i++) await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()));
        using var limited = await client.PostAsJsonAsync("/api/v1/agents/runs", new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    [Fact] public async Task ReadRateLimitIsEnforcedPerOwner()
    {
        using var client = fixture.Client(owner: Guid.NewGuid());
        for (var i = 0; i < 60; i++)
        {
            using var response = await client.GetAsync("/api/v1/agents/catalog");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await client.GetAsync("/api/v1/agents/catalog");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    [Fact] public async Task RequestLookupAndPaginatedListRemainOwnerScoped()
    {
        var owner = Guid.NewGuid(); using var client = fixture.Client(owner: owner);
        var command = new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob());
        var created = await Run(client, command);
        var found = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/requests/{command.RequestId}");
        Assert.Equal(created.GetProperty("id").GetGuid(), found.GetProperty("id").GetGuid());
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/runs?page=1&pageSize=1");
        Assert.Equal(1, page.GetArrayLength()); Assert.DoesNotContain("SQL", page.ToString()); Assert.DoesNotContain("proposal", page.ToString());
        using var invalid = await client.GetAsync("/api/v1/agents/runs?pageSize=51"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var other = fixture.Client(owner: Guid.NewGuid());
        using var hidden = await other.GetAsync($"/api/v1/agents/requests/{command.RequestId}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var empty = await other.GetFromJsonAsync<JsonElement>("/api/v1/agents/runs"); Assert.Equal(0, empty.GetArrayLength());
    }
    [Fact] public async Task HttpRecoveryRequiresExpiryAndRetainsUnknownCharge()
    {
        var owner = Guid.NewGuid();
        var run = AgentRun.Start(owner, new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()), DateTime.UtcNow);
        using var scope = fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<JobApplicationAgent.Agents.Infrastructure.PostgresAgentRuns>();
        await repo.BeginAsync(run, default); await repo.ReserveAsync(owner, run.Id, new(1500, .004m), default);
        using var live = fixture.Client(owner: owner);
        using var early = await live.PostAsync($"/api/v1/agents/runs/{run.Id}/recover", null); Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        using var later = fixture.Client(owner: owner, clock: new FutureClock(), localAuth: true);
        using var recovered = await later.PostAsync($"/api/v1/agents/runs/{run.Id}/recover", null); recovered.EnsureSuccessStatusCode();
        var result = await Body(recovered); Assert.Equal("Interrupted", result.GetProperty("failure").GetString());
        Assert.False(result.GetProperty("accounting").GetProperty("usageKnown").GetBoolean());
        Assert.Equal(1500, result.GetProperty("accounting").GetProperty("chargedTokens").GetInt64());
    }
    private sealed class FutureClock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddMinutes(6); }
    [Fact] public async Task ConfiguredOllamaAdapterFlowsThroughApiQuotaAuditAndReplay()
    {
        var owner = Guid.NewGuid(); var options = OllamaAdapterTests.Options();
        var server = new OllamaAdapterTests.Server { Completion = """{"model":"fixture:local","done":true,"done_reason":"stop","response":"{\"schemaVersion\":\"job-analysis-v1\",\"requirements\":[{\"category\":\"Skill\",\"value\":\"SQL\",\"start\":0,\"length\":3}]}","prompt_eval_count":12,"eval_count":7}""" };
        using var client = fixture.OllamaClient(owner, options, server, new OllamaAdapterTests.Tokenizer());
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/catalog"); Assert.True(catalog.GetProperty("modelAvailable").GetBoolean());
        var command = new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob());
        var result = await Run(client, command); Assert.Equal("PendingReview", result.GetProperty("state").GetString());
        Assert.Equal(options.ModelId, result.GetProperty("trace").GetProperty("modelId").GetString());
        Assert.Equal(options.TokenizerSha256, result.GetProperty("trace").GetProperty("tokenizerSha256").GetString());
        Assert.Equal(64, result.GetProperty("trace").GetProperty("preparationSha256").GetString()!.Length);
        Assert.Equal(19, result.GetProperty("accounting").GetProperty("chargedTokens").GetInt64());
        var replay = await Run(client, command); Assert.Equal(result.ToString(), replay.ToString());
        Assert.Single(server.Requests, x => x.Path == "/api/generate"); Assert.False(server.CredentialsSeen);
        var audit = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/runs/{result.GetProperty("id").GetGuid()}/audit");
        Assert.DoesNotContain("SQL required", audit.ToString()); Assert.DoesNotContain("providerPayload", audit.ToString());
    }
    [Fact] public async Task OllamaMissingUsageChargesConservativeReservationDurably()
    {
        var owner = Guid.NewGuid(); var server = new OllamaAdapterTests.Server {
            Completion = """{"model":"fixture:local","done":true,"done_reason":"stop","response":"{}","prompt_eval_count":12}""" };
        using var client = fixture.OllamaClient(owner, OllamaAdapterTests.Options(), server, new OllamaAdapterTests.Tokenizer());
        var result = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob()));
        Assert.Equal("ModelUnavailable", result.GetProperty("failure").GetString());
        Assert.False(result.GetProperty("accounting").GetProperty("usageKnown").GetBoolean());
        Assert.Equal(1500, result.GetProperty("accounting").GetProperty("chargedTokens").GetInt64());
        var budget = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget"); Assert.Equal(1500, budget.GetProperty("chargedTokens").GetInt64()); Assert.Equal(0, budget.GetProperty("heldTokens").GetInt64());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task OllamaTimeoutAccountsOnlyAfterGenerationMayHaveStarted(bool generating)
    {
        var owner = Guid.NewGuid(); var server = new OllamaAdapterTests.Server {
            BeforeRespond = (path, ct) => path == (generating ? "/api/generate" : "/api/tags") ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask };
        using var client = fixture.OllamaClient(owner, OllamaAdapterTests.Options() with { TimeoutSeconds = 1 }, server, new OllamaAdapterTests.Tokenizer());
        var result = await Run(client, new(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob()));
        Assert.Equal("TimedOut", result.GetProperty("failure").GetString());
        Assert.Equal(!generating, result.GetProperty("accounting").GetProperty("usageKnown").GetBoolean());
        Assert.Equal(generating ? 1500 : 0, result.GetProperty("accounting").GetProperty("chargedTokens").GetInt64());
        Assert.Equal(generating ? 1 : 0, server.Requests.Count(x => x.Path == "/api/generate"));
    }
}
