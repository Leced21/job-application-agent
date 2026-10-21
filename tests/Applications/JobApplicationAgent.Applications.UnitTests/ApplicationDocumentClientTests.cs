using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Applications.UnitTests;

public class ApplicationDocumentClientTests
{
    private readonly Guid owner = Guid.NewGuid(), profile = Guid.NewGuid(), job = Guid.NewGuid(), export = Guid.NewGuid();
    private IConfiguration Config(string? origin = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["Applications:Documents:BaseUrl"] = origin ?? "http://localhost:5005",
        ["Applications:Documents:Token"] = new string('t', 64) }).Build();
    private ApplicationDocumentClient Client(HttpClient http, IConfiguration? config = null) => new(http, config ?? Config());
    private Dictionary<string, object?> Receipt() => new() {
        ["exportId"] = export, ["ownerId"] = owner, ["profileId"] = profile, ["jobId"] = job, ["draftId"] = Guid.NewGuid(),
        ["kind"] = "Resume", ["fileSha256"] = new string('a', 64), ["compositionSha256"] = new string('b', 64),
        ["snapshotSha256"] = new string('c', 64), ["confirmedAtUtc"] = DateTime.UtcNow };
    [Theory]
    [InlineData("Resume")]
    [InlineData("CoverLetter")]
    public async Task SendsChannelCredential_AndBuildsTrustedReference(string kind)
    {
        var receipt = Receipt(); receipt["kind"] = kind;
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(new string('t', 64), request.Headers.Authorization?.Parameter);
            Assert.Equal($"/api/v1/documents/application-bindings/{export}", request.RequestUri!.AbsolutePath);
            Assert.False(request.Headers.Contains("Cookie")); return new(HttpStatusCode.OK) { Content = JsonContent.Create(receipt) };
        }));
        var actual = await Client(http).VerifyAsync(owner, profile, job, export, default);
        Assert.Equal(kind, actual.Kind.ToString()); Assert.Equal(new string('a', 64), actual.Sha256);
    }
    [Fact]
    public async Task AcceptsApprovedComposeDocumentsOrigin()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("documents-api", request.RequestUri!.Host);
            Assert.Equal(8080, request.RequestUri.Port);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Receipt()) };
        }));

        var actual = await Client(http, Config("http://documents-api:8080")).VerifyAsync(owner, profile, job, export, default);

        Assert.Equal(export, actual.ExportId);
    }
    [Theory]
    [InlineData("exportId")]
    [InlineData("ownerId")]
    [InlineData("profileId")]
    [InlineData("jobId")]
    [InlineData("draftId")]
    [InlineData("kind")]
    [InlineData("fileSha256")]
    [InlineData("compositionSha256")]
    [InlineData("snapshotSha256")]
    [InlineData("confirmedAtUtc")]
    public async Task RejectsMismatchedOrIncompleteReceipt(string field)
    {
        var receipt = Receipt(); receipt[field] = field.EndsWith("Id") ? Guid.Empty : field == "confirmedAtUtc" ? default(DateTime) : "invalid";
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(receipt) }));
        await Assert.ThrowsAsync<ApplicationSourceRejectedException>(() => Client(http).VerifyAsync(owner, profile, job, export, default));
    }
    [Theory]
    [InlineData(404, true)]
    [InlineData(422, true)]
    [InlineData(409, true)]
    [InlineData(401, false)]
    [InlineData(302, false)]
    [InlineData(500, false)]
    public async Task MapsRejectedSourcesSeparatelyFromUnavailableChannel(int status, bool rejected)
    {
        using var http = new HttpClient(new Handler(_ => new((HttpStatusCode)status)));
        if (rejected) await Assert.ThrowsAsync<ApplicationSourceRejectedException>(() => Client(http).VerifyAsync(owner, profile, job, export, default));
        else await Assert.ThrowsAsync<ApplicationSourceUnavailableException>(() => Client(http).VerifyAsync(owner, profile, job, export, default));
    }
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://other-api:8080")]
    [InlineData("http://documents-api:8081")]
    [InlineData("http://localhost/path")]
    public async Task InvalidConfigurationNeverCallsSource(string origin)
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Must not contact source")));
        await Assert.ThrowsAsync<ApplicationSourceUnavailableException>(() => Client(http, Config(origin)).VerifyAsync(owner, profile, job, export, default));
    }
    [Theory]
    [InlineData("invalid")]
    [InlineData("oversized")]
    public async Task InvalidOrOversizedJsonDoesNotBecomeAReference(string body)
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(body == "oversized" ? new string('a', 16385) : body) }));
        await Assert.ThrowsAsync<ApplicationSourceUnavailableException>(() => Client(http).VerifyAsync(owner, profile, job, export, default));
    }
    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create(Receipt()) }));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).VerifyAsync(owner, profile, job, export, cancelled.Token));
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
    }
}
