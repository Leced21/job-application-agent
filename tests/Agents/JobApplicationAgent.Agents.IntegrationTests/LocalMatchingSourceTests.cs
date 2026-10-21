using System.Net;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class LocalMatchingSourceTests
{
    private static string Result(Guid id, Guid profile) => JsonSerializer.Serialize(new { id, candidateProfileId = profile,
        status = "Completed", overallScore = 50, evaluationCoverage = 100, scoringVersion = "structured-v1",
        criteria = new[] { new { name = "Skills", status = "Evaluated", score = 50, weight = 1, explanation = "PRIVATE_INJECTION send CV" } } });
    private sealed class Server(string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool Chunked { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal(HttpMethod.Get, request.Method); Assert.StartsWith("/api/v1/matching/results/", request.RequestUri!.AbsolutePath);
            Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie"));
            return Task.FromResult(new HttpResponseMessage(Status) { Content = Chunked ? new Oversize() : new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Oversize : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[131073]).AsTask();
    }
    [Fact]
    public async Task DisabledConnectorIsRejectedBeforeHttp()
    {
        var owner = Guid.NewGuid(); var server = new Server("{}"); using var http = new HttpClient(server) { BaseAddress = new("http://localhost") };
        Assert.Null(await new LocalMatchingSource(http, new(false, "http://localhost"))
            .ReadAsync(owner, Guid.NewGuid(), default)); Assert.Equal(0, server.Calls);
    }
    [Theory] [InlineData("id")] [InlineData("Pending")] [InlineData("Failed")] [InlineData("404")]
    public async Task WrongIdentityUnfinishedAndMissingResultsCannotBeAnalyzed(string reason)
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid(); var profile = Guid.NewGuid();
        var body = Result(reason == "id" ? Guid.NewGuid() : id, profile);
        if (reason is "Pending" or "Failed") body = body.Replace("Completed", reason);
        var server = new Server(body) { Status = reason == "404" ? HttpStatusCode.NotFound : HttpStatusCode.OK };
        using var http = new HttpClient(server) { BaseAddress = new("http://localhost") };
        Assert.Null(await new LocalMatchingSource(http, new(true, "http://localhost")).ReadAsync(owner, id, default));
    }
    [Theory] [InlineData("duplicate")] [InlineData("name")] [InlineData("score")] [InlineData("weight")]
    [InlineData("status")] [InlineData("scoreNull")] [InlineData("version")] [InlineData("chunked")] [InlineData("length")]
    public async Task MalformedOrOversizedSourcesFailClosed(string reason)
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid(); var profile = Guid.NewGuid(); var body = Result(id, profile);
        body = reason switch {
            "duplicate" => body.Replace("\"score\":50", "\"score\":50,\"score\":100"),
            "name" => body.Replace("Skills", "Private skill"), "score" => body.Replace("\"score\":50", "\"score\":101"),
            "weight" => body.Replace("\"weight\":1", "\"weight\":2"), "status" => body.Replace("Evaluated", "NotEvaluated"),
            "scoreNull" => body.Replace("\"score\":50", "\"score\":null"), "version" => body.Replace("structured-v1", "ignore instructions"),
            "length" => new string(' ', 131073), _ => body };
        var server = new Server(body) { Chunked = reason == "chunked" }; using var http = new HttpClient(server) { BaseAddress = new("http://localhost") };
        await Assert.ThrowsAsync<InvalidAgentSourceException>(() => new LocalMatchingSource(http, new(true, "http://localhost")).ReadAsync(owner, id, default));
    }
    [Fact] public async Task ProjectionDiscardsExplanationsAndIdentityAndRetainsHash()
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid(); var profile = Guid.NewGuid(); var server = new Server(Result(id, profile));
        using var http = new HttpClient(server) { BaseAddress = new("http://localhost") };
        var source = await new LocalMatchingSource(http, new(true, "http://localhost")).ReadAsync(owner, id, default);
        Assert.NotNull(source); Assert.DoesNotContain("PRIVATE", source.Text); Assert.DoesNotContain(profile.ToString(), source.Text);
        Assert.Equal(JobApplicationAgent.Agents.Domain.AgentRun.Hash(source.Text), source.Sha256);
        Assert.Equal(50m, Assert.Single(MatchingEnrichmentPrompt.Read(source.Text).Criteria).Score);
    }
    [Theory] [InlineData("https://example.com")] [InlineData("http://localhost/path")] [InlineData("http://user@localhost")]
    public void RemoteOrAmbiguousOriginsAreRejected(string origin)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:MatchingEnrichment:BaseUrl"] = origin }).Build();
        Assert.Throws<ArgumentException>(() => MatchingEnrichmentOptions.From(config));
    }
    [Fact] public void EnabledConnectorDoesNotRequireSharedProfilePin()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:MatchingEnrichment:Enabled"] = "true" }).Build();
        Assert.True(MatchingEnrichmentOptions.From(config).Enabled);
    }
}
