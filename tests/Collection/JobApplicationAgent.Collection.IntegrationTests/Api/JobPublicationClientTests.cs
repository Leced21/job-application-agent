using System.Net;
using System.Text;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using JobApplicationAgent.Collection.Infrastructure;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class JobPublicationClientTests
{
    private sealed class Stub(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(ct);
    }
    [Theory]
    [InlineData(500, 503)]
    [InlineData(302, 503)]
    [InlineData(400, 502)]
    [InlineData(200, 502)]
    [InlineData(204, 503)]
    public async Task InvalidUpstream_ShouldReturnControlledDependencyError(int status, int expected)
    {
        using var http = new HttpClient(new Stub(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent("{}", Encoding.UTF8, "application/json") }))) { BaseAddress = new("http://localhost") };
        var exception = await Assert.ThrowsAsync<PublicationDependencyException>(() => new JobPublicationClient(http).GetAsync(Guid.NewGuid(), default));
        Assert.Equal(expected, exception.StatusCode);
    }
    [Fact]
    public async Task TimeoutAndNetworkFailure_ShouldBeRetryable_WhileCallerCancellationPropagates()
    {
        foreach (var failure in new Exception[] { new HttpRequestException(), new TaskCanceledException() })
        {
            using var http = new HttpClient(new Stub(_ => Task.FromException<HttpResponseMessage>(failure))) { BaseAddress = new("http://localhost") };
            var exception = await Assert.ThrowsAsync<PublicationDependencyException>(() => new JobPublicationClient(http).GetAsync(Guid.NewGuid(), default));
            Assert.Equal(503, exception.StatusCode);
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var cancelledHttp = new HttpClient(new Stub(ct => Task.FromCanceled<HttpResponseMessage>(ct))) { BaseAddress = new("http://localhost") };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new JobPublicationClient(cancelledHttp).GetAsync(Guid.NewGuid(), cancellation.Token));
    }
    [Fact]
    public async Task LookupMissingReceipt_ShouldReturnNull_AndConflictShouldRemainConflict()
    {
        using var missing = new HttpClient(new Stub(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))) { BaseAddress = new("http://localhost") };
        Assert.Null(await new JobPublicationClient(missing).GetAsync(Guid.NewGuid(), default));
        using var conflict = new HttpClient(new Stub(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)))) { BaseAddress = new("http://localhost") };
        await Assert.ThrowsAsync<RevisionConflictException>(() => new JobPublicationClient(conflict).GetAsync(Guid.NewGuid(), default));
    }
}
