using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationAgent.Matching.IntegrationTests.Api;

public sealed class RecommendationPaginationContractTests
{
    private sealed record PageResponse(IReadOnlyList<ItemResponse> Items, int TotalCount, int TotalPages);
    private sealed record ItemResponse(Guid JobId);

    [Theory]
    [InlineData(0, 1, 20, 0, 0)]
    [InlineData(20, 1, 20, 20, 1)]
    [InlineData(21, 2, 20, 1, 2)]
    [InlineData(21, 3, 20, 0, 2)]
    [InlineData(101, 2, 100, 1, 2)]
    public async Task Recommendations_ShouldExposeCompatibleEnvelopeAndTotals(int count, int page, int pageSize, int items, int pages)
    {
        using var factory = Factory(count);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/matching/recommendations?page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(factory.Profile!.CandidateProfileId, json.GetProperty("candidateProfileId").GetGuid());
        Assert.Equal(page, json.GetProperty("page").GetInt32());
        Assert.Equal(pageSize, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(count, json.GetProperty("totalCount").GetInt32());
        Assert.Equal(pages, json.GetProperty("totalPages").GetInt32());
        Assert.Equal(items, json.GetProperty("items").GetArrayLength());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Recommendations_ShouldCountAndReturnDuplicateJobsOnlyOnce()
    {
        using var factory = Factory(2);
        factory.Jobs = [factory.Jobs![0], factory.Jobs[0], factory.Jobs[1], factory.Jobs[1]];
        using var client = factory.CreateClient();
        var first = await client.GetFromJsonAsync<PageResponse>("/api/v1/matching/recommendations?page=1&pageSize=1");
        var second = await client.GetFromJsonAsync<PageResponse>("/api/v1/matching/recommendations?page=2&pageSize=1");
        Assert.Equal(2, first!.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.NotEqual(Assert.Single(first.Items).JobId, Assert.Single(second!.Items).JobId);
    }

    [Fact]
    public async Task Recommendations_ShouldRefusePartialResponse_WhenJobBudgetIsExceeded()
    {
        using var factory = Factory(3);
        factory.ProcessingLimits = new RecommendationProcessingLimits { MaxUniqueJobs = 2 };
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/matching/recommendations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Recommendation processing capacity exceeded", problem!.Title);
    }

    [Fact]
    public async Task Recommendations_ShouldObserveOfferChanges_OnNextRequest()
    {
        using var factory = Factory(3);
        using var client = factory.CreateClient();
        var initial = await client.GetFromJsonAsync<PageResponse>("/api/v1/matching/recommendations");
        factory.Jobs = factory.Jobs!.Take(1).ToArray();
        var updated = await client.GetFromJsonAsync<PageResponse>("/api/v1/matching/recommendations");
        Assert.Equal(3, initial!.TotalCount);
        Assert.Equal(1, updated!.TotalCount);
        Assert.Single(updated.Items);
    }

    [Fact]
    public async Task Recommendations_ShouldApplyWholeRequestDeadline_ToHttpFetch()
    {
        using var factory = Factory(0);
        factory.ProcessingLimits = new RecommendationProcessingLimits { TimeoutSeconds = 1 };
        factory.JobHttpHandler = new SlowHandler();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/matching/recommendations");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Matching service dependency timeout",
            (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static MatchingApiFactory Factory(int count) => new()
    {
        Profile = new ProfileMatchingData(Guid.NewGuid(), "Engineer", null, [], [], [], [], [], null),
        Jobs = Enumerable.Range(0, count).Select(index => new JobMatchingData(Guid.NewGuid(),
            $"Job {index}", "Company", null, "Remote", "Permanent", null, null, null, null)).ToArray()
    };
}
