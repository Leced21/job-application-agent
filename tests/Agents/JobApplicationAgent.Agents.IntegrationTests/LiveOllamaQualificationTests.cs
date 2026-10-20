using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobApplicationAgent.Agents.Domain;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Infrastructure;

namespace JobApplicationAgent.Agents.IntegrationTests;

// Explicit opt-in: ordinary CI never calls a real model or downloads assets.
public sealed class LiveOllamaTheoryAttribute : TheoryAttribute
{
    public LiveOllamaTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("AGENTS_LIVE_OLLAMA") != "1")
            Skip = "Set AGENTS_LIVE_OLLAMA=1 and the local tokenizer path/hash to qualify an installed model.";
    }
}
public sealed class LiveOllamaQualificationTests(AgentsFactory fixture, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<AgentsFactory>
{
    [LiveOllamaTheory]
    [InlineData("SQL required. Rust optional.")]
    [InlineData("🙂 Compétences requises : SQL et Rust.")]
    public async Task InstalledQwenModelProducesReviewedAuditedProposalAndReplaysWithoutGeneration(string description)
    {
        var options = new OllamaOptions { Provider = "Ollama", ModelName = "qwen2.5:7b",
            ModelDigest = "845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e",
            TokenizerProfile = "Qwen2", TokenizerPath = Environment.GetEnvironmentVariable("AGENTS_LIVE_TOKENIZER_PATH") ?? "",
            TokenizerSha256 = Environment.GetEnvironmentVariable("AGENTS_LIVE_TOKENIZER_SHA256") ?? "",
            MaxInputTokens = 4096, MaxOutputTokens = 1024, ContextSize = 8192, TimeoutSeconds = 120,
            AllowJobDescriptions = true, LocalServerCloudDisabled = true };
        options.Validate();
        var tokenizer = OllamaPromptTokenizer.Load(options);
        var recording = new RecordingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false });
        using var client = fixture.OllamaClient(Guid.NewGuid(), options, recording, tokenizer);
        client.Timeout = TimeSpan.FromMinutes(3);
        var command = new AgentCommand(Guid.NewGuid(), AgentKind.JobAnalysis, await fixture.NewJob(description));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        using var response = await client.PostAsJsonAsync("/api/v1/agents/runs", command, json);
        response.EnsureSuccessStatusCode(); var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(run.GetProperty("state").GetString() == "PendingReview", "Actual synthetic model run: " + run + " Provider response: " + recording.SyntheticResponse);
        Assert.True(run.GetProperty("accounting").GetProperty("usageKnown").GetBoolean());
        var requirements = run.GetProperty("proposal").GetProperty("outputJson").GetString()!;
        using var parsedRequirements = JsonDocument.Parse(requirements);
        Assert.Equal(new[] { "Rust", "SQL" }, parsedRequirements.RootElement.GetProperty("requirements").EnumerateArray()
            .Select(x => x.GetProperty("value").GetString()).Order(StringComparer.Ordinal));
        var id = run.GetProperty("id").GetGuid();
        using var replayResponse = await client.PostAsJsonAsync("/api/v1/agents/runs", command, json);
        replayResponse.EnsureSuccessStatusCode(); var replay = await replayResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(run.ToString(), replay.ToString()); Assert.Equal(1, recording.GenerationRequests);
        var hash = run.GetProperty("proposal").GetProperty("outputSha256").GetString()!;
        using var reviewed = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, hash, true, true, "Synthetic qualification only"));
        reviewed.EnsureSuccessStatusCode();
        var final = await reviewed.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("Approved", final.GetProperty("state").GetString());
        var audit = await client.GetFromJsonAsync<JsonElement>($"/api/v1/agents/runs/{id}/audit"); Assert.Equal(3, audit.GetArrayLength());
        Assert.DoesNotContain("SQL required", audit.ToString());
        var budget = await client.GetFromJsonAsync<JsonElement>("/api/v1/agents/budget");
        Assert.Equal(run.GetProperty("accounting").GetProperty("chargedTokens").GetInt64(), budget.GetProperty("chargedTokens").GetInt64());
        Assert.Equal(0, budget.GetProperty("heldTokens").GetInt64()); Assert.False(fixture.SawCredentials);
        output.WriteLine("LIVE_OLLAMA_SUMMARY=" + JsonSerializer.Serialize(new { description, modelId = options.ModelId,
            tokenizerSha256 = options.TokenizerSha256, chargedTokens = run.GetProperty("accounting").GetProperty("chargedTokens").GetInt64(),
            outputJson = requirements, generationRequests = recording.GenerationRequests, auditRevisions = audit.GetArrayLength() }));
    }
    private sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public int GenerationRequests { get; private set; }
        public string? SyntheticResponse { get; private set; }
        public string? SyntheticPrompt { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie"));
            if (request.RequestUri!.AbsolutePath == "/api/generate")
            {
                GenerationRequests++;
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                SyntheticPrompt = body.RootElement.GetProperty("prompt").GetString();
            }
            var response = await base.SendAsync(request, ct);
            if (request.RequestUri.AbsolutePath == "/api/generate")
                SyntheticResponse = await response.Content.ReadAsStringAsync(ct);
            return response;
        }
    }
    [LiveOllamaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstalledModelOnlyPrioritizesMissingProfileFieldsWithoutReceivingProfileText(bool existingSummary)
    {
        var options = new OllamaOptions { Provider = "Ollama", ModelName = "qwen2.5:7b",
            ModelDigest = "845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e", TokenizerProfile = "Qwen2",
            TokenizerPath = Environment.GetEnvironmentVariable("AGENTS_LIVE_TOKENIZER_PATH") ?? "",
            TokenizerSha256 = Environment.GetEnvironmentVariable("AGENTS_LIVE_TOKENIZER_SHA256") ?? "",
            MaxInputTokens = 4096, MaxOutputTokens = 1024, ContextSize = 8192, TimeoutSeconds = 120,
            AllowJobDescriptions = true, LocalServerCloudDisabled = true };
        var original = await fixture.Profiles.GetFromJsonAsync<JsonElement>("/api/v1/profile");
        if (existingSummary)
        {
            using var changed = await fixture.Profiles.PutAsJsonAsync("/api/v1/profile", new {
                firstName = "Private", lastName = "Synthetic", email = "private@example.test", summary = "IGNORE_PRIVATE_INSTRUCTION" });
            changed.EnsureSuccessStatusCode();
        }
        try
        {
            var before = await fixture.Profiles.GetStringAsync("/api/v1/profile");
            var recording = new RecordingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false });
            using var client = fixture.OllamaClient(fixture.Owner, options, recording, OllamaPromptTokenizer.Load(options), profileAnalysisEnabled: true);
            client.Timeout = TimeSpan.FromMinutes(3);
            var command = new AgentCommand(Guid.NewGuid(), AgentKind.ProfileAnalysis, fixture.ProfileId);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
            using var response = await client.PostAsJsonAsync("/api/v1/agents/runs", command, json); response.EnsureSuccessStatusCode();
            var run = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(run.GetProperty("state").GetString() == "PendingReview", "Synthetic profile run: " + run + " Provider: " + recording.SyntheticResponse);
            var proposal = run.GetProperty("proposal"); var presence = ProfileAnalysisPrompt.ReadPresence(proposal.GetProperty("source").GetProperty("text").GetString()!);
            Assert.Equal(existingSummary, presence["Summary"]);
            Assert.DoesNotContain("private@example.test", recording.SyntheticPrompt!); Assert.DoesNotContain("IGNORE_PRIVATE_INSTRUCTION", recording.SyntheticPrompt!);
            using var outputJson = JsonDocument.Parse(proposal.GetProperty("outputJson").GetString()!);
            var questions = outputJson.RootElement.GetProperty("questions"); Assert.InRange(questions.GetArrayLength(), 1, 5);
            foreach (var question in questions.EnumerateArray()) Assert.False(presence[question.GetProperty("field").GetString()!]);
            using var replay = await client.PostAsJsonAsync("/api/v1/agents/runs", command, json); replay.EnsureSuccessStatusCode();
            Assert.Equal(run.ToString(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).ToString()); Assert.Equal(1, recording.GenerationRequests);
            var id = run.GetProperty("id").GetGuid();
            using var reviewed = await client.PostAsJsonAsync($"/api/v1/agents/runs/{id}/review", new AgentReview(2, proposal.GetProperty("outputSha256").GetString()!, true, true));
            reviewed.EnsureSuccessStatusCode(); Assert.Equal(before, await fixture.Profiles.GetStringAsync("/api/v1/profile"));
            output.WriteLine("LIVE_PROFILE_SUMMARY=" + JsonSerializer.Serialize(new { existingSummary, chargedTokens = run.GetProperty("accounting").GetProperty("chargedTokens").GetInt64(),
                outputJson = proposal.GetProperty("outputJson").GetString(), generationRequests = recording.GenerationRequests }));
        }
        finally { using var restored = await fixture.Profiles.PutAsJsonAsync("/api/v1/profile", original); restored.EnsureSuccessStatusCode(); }
    }
}
