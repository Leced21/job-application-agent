using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class MatchingEnrichmentApiTests(AgentsFactory fixture) : IClassFixture<AgentsFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task<JsonElement> Run(HttpClient client, AgentCommand command)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/agents/runs", command, Json);
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RealMatchingApiIsReadOnlyAndMinimizedWithReplayReviewAndPersistentBudget(bool unassessed)
    {
        var owner = fixture.Owner; var resource = await fixture.NewMatching(unassessed: unassessed);
        var before = await fixture.Matching.GetStringAsync($"/api/v1/matching/results/{resource}");
        using var client = fixture.MatchingClient(owner);
        var chargedBefore = (await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget")).GetProperty("chargedTokens").GetInt64();
        var command = new AgentCommand(Guid.NewGuid(), AgentKind.MatchingEnrichment, resource);
        var run = await Run(client, command); Assert.Equal("PendingReview", run.GetProperty("state").GetString());
        var proposal = run.GetProperty("proposal"); Assert.Equal(MatchingEnrichmentPrompt.Version, proposal.GetProperty("promptVersion").GetString());
        var source = proposal.GetProperty("source").GetProperty("text").GetString()!;
        Assert.DoesNotContain("PRIVATE", source); Assert.DoesNotContain("Private", source); Assert.DoesNotContain(fixture.ProfileId.ToString(), source);
        Assert.DoesNotContain("Private", fixture.Model.LastRequest!.Prompt.UntrustedDataJson);
        var snapshot = MatchingEnrichmentPrompt.Read(source); Assert.Equal(3, snapshot.Criteria.Length);
        using var output = JsonDocument.Parse(proposal.GetProperty("outputJson").GetString()!);
        var skills = output.RootElement.GetProperty("observations").EnumerateArray().Single(item => item.GetProperty("criterion").GetString() == "Skills");
        Assert.Equal(unassessed ? "InformationMissing" : "PartialMatchReported", skills.GetProperty("focus").GetString());
        var calls = fixture.Model.Calls;
        using var restarted = fixture.MatchingClient(owner);
        Assert.Equal(run.ToString(), (await Run(restarted, command)).ToString()); Assert.Equal(calls, fixture.Model.Calls);
        var id = run.GetProperty("id").GetGuid();
        using var reviewed = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, proposal.GetProperty("outputSha256").GetString()!, true, true));
        reviewed.EnsureSuccessStatusCode(); Assert.Equal("Approved", (await reviewed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
        Assert.Equal(before, await fixture.Matching.GetStringAsync($"/api/v1/matching/results/{resource}"));
        var budget = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget"); Assert.Equal(chargedBefore + 140, budget.GetProperty("chargedTokens").GetInt64());
        Assert.Equal(0, budget.GetProperty("heldTokens").GetInt64()); Assert.True(fixture.SawCredentials);
        var audit = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/runs/{id}/audit"); Assert.Equal(3, audit.GetArrayLength());
        Assert.DoesNotContain("Private", audit.ToString()); Assert.DoesNotContain("observations", audit.ToString());
        using var other = fixture.MatchingClient(Guid.NewGuid());
        using var hidden = await other.GetAsync($"/api/v1/agents/runs/{id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DisabledOrOtherProfileDoesNotCallModel(bool otherProfile)
    {
        using var client = fixture.MatchingClient(fixture.Owner, enabled: otherProfile);
        var id = await fixture.NewMatching(profileId: otherProfile ? Guid.NewGuid() : fixture.ProfileId);
        var before = fixture.Model.Calls;
        var run = await Run(client, new(Guid.NewGuid(), AgentKind.MatchingEnrichment, id));
        Assert.Equal("SourceRejected", run.GetProperty("failure").GetString()); Assert.Equal(before, fixture.Model.Calls);
        Assert.Equal(0, run.GetProperty("accounting").GetProperty("chargedTokens").GetInt64());
    }
    [Fact] public async Task CatalogSeparatesImplementationFromConfiguredAccess()
    {
        using var disabled = fixture.Client();
        var catalog = await disabled.GetFromJsonAsync<JsonElement>("/api/v1/agents/catalog"); Assert.False(catalog.GetProperty("matchingEnrichmentEnabled").GetBoolean());
        Assert.Equal(4, catalog.GetProperty("matchingMessages").EnumerateObject().Count());
        using var enabled = fixture.MatchingClient(Guid.NewGuid());
        Assert.True((await enabled.GetFromJsonAsync<JsonElement>("/api/v1/agents/catalog")).GetProperty("matchingEnrichmentEnabled").GetBoolean());
    }
}
