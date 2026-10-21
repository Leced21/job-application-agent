using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;

namespace JobApplicationAgent.Matching.UnitTests.Infrastructure.HttpClients;

public sealed class RecommendableJobHttpClientTests
{
    private const string ValidPage = """
        [{"id":"11111111-1111-1111-1111-111111111111","title":"Engineer",
          "companyName":"Company","location":null,"workMode":"Remote",
          "contractType":"Permanent","salaryMin":null,"salaryMax":null,
          "salaryCurrency":null,"description":null,"status":"Saved"}]
        """;

    [Fact]
    public async Task GetRecommendablePageAsync_ShouldUseDedicatedEndpoint_AndReadNullableFields()
    {
        Uri? requestedUri = null;
        using var http = CreateHttp((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(Response(HttpStatusCode.OK, ValidPage));
        });

        var jobs = await new JobMatchingHttpClient(http).GetRecommendablePageAsync(2, 10);

        Assert.Equal("/api/v1/jobs/recommendable?page=2&pageSize=10", requestedUri!.PathAndQuery);
        var job = Assert.Single(jobs);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), job.Id);
        Assert.Equal("Engineer", job.Title);
        Assert.Equal("Remote", job.WorkMode);
        Assert.Equal("Permanent", job.ContractType);
        Assert.Null(job.SalaryMin);
        Assert.Null(job.Description);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1_000_000, 100)]
    public async Task GetRecommendablePageAsync_ShouldAcceptBounds_AndEmptyPage(int page, int pageSize)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "[]")));
        Assert.Empty(await new JobMatchingHttpClient(http).GetRecommendablePageAsync(page, pageSize));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1_000_001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task GetRecommendablePageAsync_ShouldRejectPagination_BeforeSending(int page, int pageSize)
    {
        var calls = 0;
        using var http = CreateHttp((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(HttpStatusCode.OK, "[]"));
        });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(page, pageSize));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetRecommendablePageAsync_ShouldRejectHttpErrors(HttpStatusCode status)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(Response(status, "[]")));
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20));
        Assert.Equal(status, exception.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("[1]")]
    public async Task GetRecommendablePageAsync_ShouldRejectInvalidPayloads(string json)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.OK, json)));
        await Assert.ThrowsAsync<JsonException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20));
    }

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("companyName")]
    [InlineData("workMode")]
    [InlineData("contractType")]
    public async Task GetRecommendablePageAsync_ShouldRejectMissingRequiredFields(string field)
    {
        var json = JsonNode.Parse(ValidPage)!;
        json[0]!.AsObject().Remove(field);
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.OK, json.ToJsonString())));
        await Assert.ThrowsAsync<JsonException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20));
    }

    [Fact]
    public async Task GetRecommendablePageAsync_ShouldRejectPageLargerThanRequested()
    {
        var json = JsonNode.Parse(ValidPage)!.AsArray();
        json.Add(json[0]!.DeepClone());
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.OK, json.ToJsonString())));
        await Assert.ThrowsAsync<JsonException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 1));
    }

    [Fact]
    public async Task GetRecommendablePageAsync_ShouldPropagateTransportErrors()
    {
        var failure = new HttpRequestException("Connection refused");
        using var http = CreateHttp((_, _) => Task.FromException<HttpResponseMessage>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<HttpRequestException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20)));
    }

    [Fact]
    public async Task GetRecommendablePageAsync_ShouldCancelInFlightRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = CreateHttp(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Response(HttpStatusCode.OK, "[]");
        });
        var pending = new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20, cancellation.Token);
        await started.Task;
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task GetRecommendablePageAsync_ShouldHonorHttpTimeout()
    {
        using var http = CreateHttp(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Response(HttpStatusCode.OK, "[]");
        });
        http.Timeout = TimeSpan.FromMilliseconds(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new JobMatchingHttpClient(http).GetRecommendablePageAsync(1, 20));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpClient CreateHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new DelegateHandler(send)) { BaseAddress = new Uri("http://job.test/") };

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
