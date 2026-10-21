using System.Net;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using JobApplicationAgent.Agents.Infrastructure;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class OllamaAdapterTests
{
    private static readonly ModelPrompt Prompt = new("Trusted instructions", "{\"untrustedJobText\":\"SQL\"}", "{\"type\":\"object\"}");
    [Theory]
    [InlineData("http://127.0.0.1:11434/")]
    [InlineData("http://agents-ollama:11434/")]
    public void ModelOriginAllowsLoopbackAndPrivateOllamaService(string origin) => Assert.Equal(origin, OllamaOptions.ModelOrigin(origin).AbsoluteUri);
    [Theory]
    [InlineData("http://example.com:11434/")]
    [InlineData("http://agents-ollama:11435/")]
    [InlineData("http://agents-ollama:11434/api/")]
    [InlineData("http://user@agents-ollama:11434/")]
    public void ModelOriginRejectsNonLocalOrAmbiguousAddresses(string origin) => Assert.Throws<ArgumentException>(() => OllamaOptions.ModelOrigin(origin));
    internal static OllamaOptions Options() => new() { Provider = "Ollama", ModelName = "fixture:local", ModelDigest = new('a', 64),
        TokenizerProfile = "Qwen2", TokenizerPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fixture.tiktoken")),
        TokenizerSha256 = new('b', 64), MaxInputTokens = 1000, MaxOutputTokens = 500, ContextSize = 2048, TimeoutSeconds = 10,
        AllowJobDescriptions = true, LocalServerCloudDisabled = true };
    internal sealed class Tokenizer : IOllamaPromptTokenizer
    { public string Render(ModelPrompt prompt) => JsonSerializer.Serialize(prompt); public int CountTokens(string text) => 12; }
    internal sealed class Server : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];
        public Func<string, string>? Override { get; set; }
        public Func<string, CancellationToken, Task>? BeforeRespond { get; set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool UnknownLengthOversize { get; set; }
        public bool CredentialsSeen { get; private set; }
        public string Completion { get; set; } = """{"model":"fixture:local","done":true,"done_reason":"stop","response":"{}","prompt_eval_count":12,"eval_count":7}""";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((path, body)); CredentialsSeen |= request.Headers.Authorization is not null || request.Headers.Contains("Cookie");
            if (BeforeRespond is not null) await BeforeRespond(path, ct);
            var json = path switch {
                "/api/status" => """{"cloud":{"disabled":true,"source":"env"}}""",
                "/api/tags" => $$"""{"models":[{"name":"fixture:local","digest":"{{new string('a', 64)}}"}]}""",
                "/api/show" => """{"capabilities":["completion"],"model_info":{"general.architecture":"qwen2","tokenizer.ggml.model":"gpt2","tokenizer.ggml.pre":"qwen2","tokenizer.ggml.add_bos_token":false,"qwen2.context_length":8192}}""",
                "/api/generate" => Completion, _ => throw new Xunit.Sdk.XunitException("Unexpected provider route.") };
            return new(Status) { Content = UnknownLengthOversize ? new Oversize() : new StringContent(Override?.Invoke(json) ?? json, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Oversize : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[2097153]).AsTask();
    }
    private static (OllamaAgentModel Model, HttpClient Http) Create(Server server)
    {
        var http = new HttpClient(server) { BaseAddress = new("http://127.0.0.1:11434") };
        return (new(http, new Tokenizer(), Options()), http);
    }
    private static ModelRequest Request(PreparedModelInput input) => new(Options().ModelId, Prompt, 1000, 500, input);
    [Fact] public async Task PreparesWithoutGeneratingThenSendsExactPromptSchemaAndCaps()
    {
        var server = new Server(); var (model, http) = Create(server); using var _ = http;
        var prepared = await model.PrepareAsync(Options().ModelId, Prompt, default);
        Assert.Equal(12, prepared.InputTokens); Assert.Equal(3, server.Requests.Count); Assert.DoesNotContain(server.Requests, x => x.Path == "/api/generate");
        var result = await model.GenerateAsync(Request(prepared), default);
        Assert.Null(result.Failure); Assert.Equal(12, result.InputTokens); Assert.Equal(7, result.OutputTokens);
        using var body = JsonDocument.Parse(server.Requests.Single(x => x.Path == "/api/generate").Body); var root = body.RootElement;
        Assert.True(root.GetProperty("raw").GetBoolean()); Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.False(root.GetProperty("think").GetBoolean()); Assert.False(root.GetProperty("truncate").GetBoolean());
        Assert.Equal("30m", root.GetProperty("keep_alive").GetString());
        Assert.Equal(prepared.ProviderPayload, root.GetProperty("prompt").GetString()); Assert.Equal(JsonValueKind.Object, root.GetProperty("format").ValueKind);
        Assert.Equal(500, root.GetProperty("options").GetProperty("num_predict").GetInt32()); Assert.Equal(2048, root.GetProperty("options").GetProperty("num_ctx").GetInt32());
        Assert.False(server.CredentialsSeen); Assert.DoesNotContain(server.Requests, x => x.Path.Contains("pull"));
    }
    [Theory] [InlineData("digest")] [InlineData("remote")] [InlineData("architecture")] [InlineData("bos")] [InlineData("context")] [InlineData("tokenizer")]
    public async Task UnsupportedMetadataPreventsGeneration(string reason)
    {
        var server = new Server { Override = json => reason switch {
            "digest" => json.Replace(new string('a', 64), new string('c', 64)),
            "remote" => json.Replace("\"capabilities\"", "\"remote_host\":\"https://example.test\",\"capabilities\""),
            "architecture" => json.Replace("qwen2\"", "llama\""), "bos" => json.Replace("false", "true"),
            "context" => json.Replace("8192", "512"), _ => json.Replace("gpt2", "unsupported") } };
        var (model, http) = Create(server); using var _ = http;
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.PrepareAsync(Options().ModelId, Prompt, default));
        Assert.DoesNotContain(server.Requests, x => x.Path == "/api/generate");
    }
    [Fact] public async Task AliasChangeAfterPreparationIsProvenNotInvoked()
    {
        var server = new Server(); var (model, http) = Create(server); using var _ = http;
        var prepared = await model.PrepareAsync(Options().ModelId, Prompt, default);
        server.Override = json => json.Replace(new string('a', 64), new string('c', 64));
        await Assert.ThrowsAsync<AgentModelNotInvokedException>(() => model.GenerateAsync(Request(prepared), default));
        Assert.DoesNotContain(server.Requests, x => x.Path == "/api/generate");
    }
    [Theory] [InlineData("prompt")] [InlineData("count")] [InlineData("model")] [InlineData("cap")] [InlineData("hash")]
    public async Task ModifiedPreparedRequestCannotGenerate(string field)
    {
        var server = new Server(); var (model, http) = Create(server); using var _ = http;
        var prepared = await model.PrepareAsync(Options().ModelId, Prompt, default);
        var request = field switch {
            "prompt" => Request(prepared with { ProviderPayload = "modified" }), "count" => Request(prepared with { InputTokens = 1 }),
            "model" => Request(prepared) with { ModelId = "another" }, "cap" => Request(prepared) with { MaxOutputTokens = 501 },
            _ => Request(prepared with { TokenizerSha256 = new string('c', 64) }) };
        await Assert.ThrowsAsync<AgentModelNotInvokedException>(() => model.GenerateAsync(request, default));
        Assert.DoesNotContain(server.Requests, x => x.Path == "/api/generate");
    }
    [Theory] [InlineData("length")] [InlineData("thinking")] [InlineData("tools")] [InlineData("count")]
    public async Task RejectedResponsePreservesKnownUsage(string field)
    {
        var server = new Server(); var (model, http) = Create(server); using var _ = http;
        var prepared = await model.PrepareAsync(Options().ModelId, Prompt, default);
        server.Completion = field switch {
            "length" => server.Completion.Replace("stop", "length"), "thinking" => server.Completion.Replace("\"done\":true", "\"done\":true,\"thinking\":\"internal\""),
            "tools" => server.Completion.Replace("\"done\":true", "\"done\":true,\"tool_calls\":[]"), _ => server.Completion.Replace(":12", ":13") };
        var result = await model.GenerateAsync(Request(prepared), default);
        Assert.Equal(field == "count" ? AgentFailure.UsageViolation : AgentFailure.InvalidOutput, result.Failure); Assert.Equal(7, result.OutputTokens); Assert.Empty(result.OutputJson);
    }
    [Theory] [InlineData("missing")] [InlineData("negative")] [InlineData("model")] [InlineData("incomplete")] [InlineData("duplicate")]
    public async Task UntrustedOrMissingUsageFailsWithoutGuessing(string field)
    {
        var server = new Server(); var (model, http) = Create(server); using var _ = http;
        var prepared = await model.PrepareAsync(Options().ModelId, Prompt, default);
        server.Completion = field switch {
            "missing" => server.Completion.Replace(",\"eval_count\":7", ""), "negative" => server.Completion.Replace("\"eval_count\":7", "\"eval_count\":-1"),
            "model" => server.Completion.Replace("fixture:local", "other"), "duplicate" => server.Completion.Replace("\"done\":true", "\"done\":true,\"done\":false"),
            _ => server.Completion.Replace("\"done\":true", "\"done\":false") };
        await Assert.ThrowsAnyAsync<Exception>(() => model.GenerateAsync(Request(prepared), default));
        Assert.Single(server.Requests, x => x.Path == "/api/generate");
    }
    [Fact] public async Task ChunkedOversizeMetadataCannotGenerate()
    {
        var server = new Server { UnknownLengthOversize = true }; var (model, http) = Create(server); using var _ = http;
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.PrepareAsync(Options().ModelId, Prompt, default));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task RuntimeCloudMustBeExplicitlyDisabled(bool missing)
    {
        var server = new Server { Override = json => json.Contains("\"cloud\"") ? (missing ? "{}" : """{"cloud":{"disabled":false}}""") : json };
        var (model, http) = Create(server); using var _ = http;
        await Assert.ThrowsAnyAsync<Exception>(() => model.PrepareAsync(Options().ModelId, Prompt, default));
        Assert.Single(server.Requests); Assert.DoesNotContain(server.Requests, x => x.Path == "/api/generate");
    }
    [Theory] [InlineData(HttpStatusCode.TemporaryRedirect)] [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task TransportErrorsDoNotEchoProviderBody(HttpStatusCode status)
    {
        var server = new Server { Status = status, Override = _ => "sensitive source" }; var (model, http) = Create(server); using var _ = http;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => model.PrepareAsync(Options().ModelId, Prompt, default));
        Assert.DoesNotContain("sensitive", error.ToString()); Assert.Single(server.Requests);
    }
}
