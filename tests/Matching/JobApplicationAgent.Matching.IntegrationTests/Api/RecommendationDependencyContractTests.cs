using System.Net;
using System.Net.Http.Json;
using System.Text;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationAgent.Matching.IntegrationTests.Api;

public sealed class RecommendationDependencyContractTests
{
    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    public async Task Recommendations_ShouldReturn502_WhenRealHttpClientReceivesInvalidJobResponse(string payload)
    {
        using var factory = CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/matching/recommendations");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Matching service dependency invalid response", problem!.Title);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Recommendations_ShouldReturn503_WhenJobEndpointFails(HttpStatusCode status)
    {
        using var factory = CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/matching/recommendations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Recommendations_ShouldReturn503_WhenJobTransportFails()
    {
        using var factory = CreateFactory((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused")));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.GetAsync("/api/v1/matching/recommendations")).StatusCode);
    }

    [Fact]
    public async Task Recommendations_ShouldReturn503_WhenJobHttpRequestTimesOut()
    {
        using var factory = CreateFactory((_, _) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Dependency timeout")));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/matching/recommendations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Matching service dependency timeout",
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Recommendations_ShouldRequestEligibleRoute_AndAcceptEmptyArray()
    {
        Uri? requestedUri = null;
        using var factory = CreateFactory((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        });
        using var client = factory.CreateClient();
        var response = await client.GetFromJsonAsync<RecommendationResponse>(
            "/api/v1/matching/recommendations?page=2&pageSize=10");
        Assert.Empty(response!.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal("/api/v1/jobs/recommendable?page=1&pageSize=100", requestedUri!.PathAndQuery);
    }

    private static MatchingApiFactory CreateFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) => new()
    {
        Profile = new ProfileMatchingData(Guid.NewGuid(), "Engineer", null, [], [], [], [], [], null),
        JobHttpHandler = new DelegateHandler(send)
    };

    private sealed class DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
