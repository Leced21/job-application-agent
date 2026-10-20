using System.Net;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Infrastructure;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class LocalJobSourceTests
{
    [Theory]
    [InlineData("https://example.test")]
    [InlineData("http://localhost:5002/api/")]
    [InlineData("http://user:secret@localhost:5002")]
    [InlineData("http://localhost:5002/?secret=value")]
    [InlineData("http://localhost:5002/#fragment")]
    [InlineData("file:///tmp/job")]
    public void RejectsUnsafeOrigins(string origin) => Assert.Throws<ArgumentException>(() => LocalJobSource.ValidateOrigin(origin));
    [Theory] [InlineData("http://localhost:5002")] [InlineData("http://127.0.0.1:5002/")] [InlineData("https://[::1]:5002")]
    public void AcceptsLoopbackOrigins(string origin) => Assert.True(LocalJobSource.ValidateOrigin(origin).IsLoopback);
    [Fact] public void AcceptsOnlyTheJobComposeOrigin() => Assert.Equal("job-api", LocalJobSource.ValidateOrigin("http://job-api:8080/").Host);
    [Theory] [InlineData("http://profile-api:8080/")] [InlineData("http://job-api:8081/")] [InlineData("https://job-api:8080/")]
    public void RejectsNonJobComposeOrigins(string origin) => Assert.Throws<ArgumentException>(() => LocalJobSource.ValidateOrigin(origin));
    [Theory] [InlineData("Ignored")] [InlineData("Expired")] [InlineData("Unknown")]
    public async Task RejectsUnavailableJobStatuses(string status)
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        using var client = Client(new StringContent(JsonSerializer.Serialize(new { id, status, description = "SQL" })));
        Assert.Null(await new LocalJobSource(client).ReadAsync(owner, id, default));
    }
    [Fact] public async Task PublicJobSourceReturnsDataForTheRequestedOwner()
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        using var client = Client(new StringContent(JsonSerializer.Serialize(new { id, status = "Saved", description = "SQL" })));
        var result = await new LocalJobSource(client).ReadAsync(owner, id, default);
        Assert.Equal(owner, result?.OwnerId);
    }
    [Fact] public async Task RejectsMismatchedResourceIdentity()
    {
        var owner = Guid.NewGuid(); using var client = Client(new StringContent(JsonSerializer.Serialize(new { id = Guid.NewGuid(), status = "Saved", description = "SQL" })));
        Assert.Null(await new LocalJobSource(client).ReadAsync(owner, Guid.NewGuid(), default));
    }
    [Fact] public async Task BoundsChunkedResponsesWithoutContentLength()
    {
        var owner = Guid.NewGuid(); using var client = Client(new StreamingContent());
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalJobSource(client).ReadAsync(owner, Guid.NewGuid(), default));
    }
    [Fact] public async Task DoesNotReadRedirectBodyAsAJob()
    {
        var owner = Guid.NewGuid(); using var client = new HttpClient(new ResponseHandler(new HttpResponseMessage(HttpStatusCode.Redirect) {
            Headers = { Location = new Uri("https://example.test/secret") } })) { BaseAddress = new("http://localhost:5002") };
        await Assert.ThrowsAsync<HttpRequestException>(() => new LocalJobSource(client).ReadAsync(owner, Guid.NewGuid(), default));
    }
    private static HttpClient Client(HttpContent content) => new(new ResponseHandler(new(HttpStatusCode.OK) { Content = content })) { BaseAddress = new("http://localhost:5002") };
    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(response); }
    private sealed class NeverHandler : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new Xunit.Sdk.XunitException("HTTP must not be invoked."); }
    private sealed class StreamingContent : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(new string('x', 131073))).AsTask();
    }
}
