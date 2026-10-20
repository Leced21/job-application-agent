
using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.IntegrationTests.Infrastructure;
using NSubstitute;

namespace JobApplicationAgent.Matching.IntegrationTests.Api;

public sealed class MatchingControllerTests
{
    private static readonly Guid JobId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid ProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed record RecommendationApiResponse(
        Guid CandidateProfileId,
        IReadOnlyList<JobRecommendationApiResponse> Items,
        int Page,
        int PageSize);

    private sealed record JobRecommendationApiResponse(
        Guid JobId,
        string Title,
        string CompanyName,
        string? Location,
        string WorkMode,
        string ContractType,
        decimal? SalaryMin,
        decimal? SalaryMax,
        string? SalaryCurrency,
        decimal? OverallScore,
        decimal? RankingScore,
        decimal EvaluationCoverage,
        string Status,
        IReadOnlyCollection<MatchingCriterionApiResponse> Criteria);

    private sealed record MatchingCriterionApiResponse(
        string Name,
        decimal? Score,
        decimal Weight,
        string Explanation,
        int Status);

    [Fact]
    public async Task Calculate_ShouldReturn200_WhenMatchingSucceeds()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/matching/calculate",
            new { jobId = JobId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MatchingResponse>();

        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(80m, result.OverallScore);
        Assert.Equal(JobId, result.JobId);
        Assert.Equal(ProfileId, result.CandidateProfileId);
        Assert.Single(result.Criteria);
        Assert.Equal(100m, result.EvaluationCoverage);
    }

    [Fact]
    public async Task Calculate_ShouldReturn400_WhenJobIdIsEmpty()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/matching/calculate", new { jobId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_ShouldReturn404_WhenJobDoesNotExist()
    {
        using var factory = CreateFactory();
        factory.Job = null;

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/matching/calculate", new { jobId = JobId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_ShouldReturn404_WhenProfileDoesNotExist()
    {
        using var factory = CreateFactory();
        factory.Profile = null;

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/matching/calculate",
            new { jobId = JobId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Calculate_ShouldReturnInsufficientData_WhenNoCriterionIsEvaluable()
    {
        using var factory = CreateFactory();

        factory.Criteria =
        [
            MatchingCriterion.NotEvaluated(
                "Skills",
                0.5m,
                "Requirements not extracted."),

            MatchingCriterion.NotEvaluated(
                "Location",
                0.5m,
                "Preferences unavailable.")
        ];

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/matching/calculate",
            new { jobId = JobId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MatchingResponse>();

        Assert.NotNull(result);
        Assert.Equal("InsufficientData", result.Status);
        Assert.Null(result.OverallScore);
        Assert.Equal(2, result.Criteria.Count);
        Assert.Equal(0m, result.EvaluationCoverage);
    }

    [Fact]
    public async Task Calculate_ShouldReturn503_WhenJobServiceIsUnavailable()
    {
        using var factory = CreateFactory();

        factory.JobClientException =
            new HttpRequestException("Job Service unavailable.");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/matching/calculate",
            new { jobId = JobId });

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
    }

    [Fact]
    public async Task Calculate_ShouldReturn503_WhenProfileServiceTimesOut()
    {
        using var factory = CreateFactory();

        factory.ProfileClientException = new TaskCanceledException("Profile Service timeout.");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/matching/calculate", new { jobId = JobId });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn200_WhenMatchingExists()
    {
        using var factory = CreateFactory();

        var matchingId = Guid.NewGuid();

        var expected = new MatchingResultDetails(
            matchingId,
            ProfileId,
            JobId,
            "Completed",
            90m,
            100m,
            [
                new MatchingCriterionDetails(
                "JobTitle",
                100m,
                0.30m,
                "Evaluated",
                "Job title matches.")
            ],
            DateTime.UtcNow,
            DateTime.UtcNow);

        factory.MatchingRepository
            .GetByIdAsync(
                matchingId,
                Arg.Any<CancellationToken>())
            .Returns(expected);

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results/{matchingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MatchingResultDetails>();

        Assert.NotNull(result);
        Assert.Equal(matchingId, result.Id);
        Assert.Equal(90m, result.OverallScore);
        Assert.Single(result.Criteria);
    }

    [Fact]
    public async Task GetById_ShouldReturn404_WhenMatchingDoesNotExist()
    {
        using var factory = CreateFactory();

        var matchingId = Guid.NewGuid();

        factory.MatchingRepository
            .GetByIdAsync(
                matchingId,
                Arg.Any<CancellationToken>())
            .Returns((MatchingResultDetails?)null);

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results/{matchingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ShouldReturn400_WhenMatchingIdIsEmpty()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results/{Guid.Empty}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await factory.MatchingRepository
            .DidNotReceive()
            .GetByIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_ShouldReturn200_WhenResultsExist()
    {
        using var factory = CreateFactory();

        var filter = new MatchingResultFilter(
            ProfileId, JobId, "Completed", 1, 20);

        var expected = new MatchingResultPage(
            [
                new MatchingResultSummary(
                Guid.NewGuid(),
                ProfileId,
                JobId,
                "Completed",
                90m,
                100m,
                DateTime.UtcNow,
                DateTime.UtcNow)
            ],
            1, 1, 20, 1);

        factory.MatchingRepository
            .SearchAsync(filter, Arg.Any<CancellationToken>())
            .Returns(expected);

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results" +
            $"?candidateProfileId={ProfileId}" +
            $"&jobId={JobId}" +
            "&status=Completed&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MatchingResultPage>();

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(90m, result.Items.Single().OverallScore);

        await factory.MatchingRepository.Received(1)
            .SearchAsync(filter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_ShouldReturnEmptyPage_WhenNoResultsExist()
    {
        using var factory = CreateFactory();

        var filter = new MatchingResultFilter(
            ProfileId, null, null, 1, 20);

        factory.MatchingRepository
            .SearchAsync(filter, Arg.Any<CancellationToken>())
            .Returns(new MatchingResultPage(
                Array.Empty<MatchingResultSummary>(),
                0, 1, 20, 0));

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results?candidateProfileId={ProfileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<MatchingResultPage>();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Search_ShouldSupportPagination()
    {
        using var factory = CreateFactory();

        var filter = new MatchingResultFilter(ProfileId, null, null, 2, 1);

        factory.MatchingRepository
            .SearchAsync(filter, Arg.Any<CancellationToken>())
            .Returns(new MatchingResultPage(
                [
                    new MatchingResultSummary(
                    Guid.NewGuid(),
                    ProfileId,
                    JobId,
                    "Completed",
                    90m,
                    100m,
                    DateTime.UtcNow,
                    DateTime.UtcNow)
                ],
                2, 2, 1, 2));

        using var client = factory.CreateClient();

        var response = await client.GetAsync( $"/api/v1/matching/results" + $"?candidateProfileId={ProfileId}&page=2&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<MatchingResultPage>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Recommendations_ShouldReturn200_WithMatchingResults()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/matching/recommendations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<RecommendationApiResponse>();

        Assert.NotNull(result);
        Assert.Equal(ProfileId, result.CandidateProfileId);
        Assert.Single(result.Items);
        Assert.Equal(JobId, result.Items[0].JobId);
        Assert.Equal(80m, result.Items[0].OverallScore);
        Assert.Equal(100m, result.Items[0].EvaluationCoverage);
        Assert.Equal("Completed", result.Items[0].Status);
        Assert.Single(result.Items[0].Criteria);
    }

    [Fact]
    public async Task Recommendations_ShouldReturn404_WhenProfileDoesNotExist()
    {
        using var factory = CreateFactory();
        factory.Profile = null;

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/matching/recommendations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, factory.JobPageCallCount);
    }

    [Fact]
    public async Task Recommendations_ShouldReturnEmptyList_WhenNoJobsExist()
    {
        using var factory = CreateFactory();
        factory.Jobs = [];

        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/matching/recommendations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<RecommendationResponse>();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(ProfileId, result.CandidateProfileId);
    }

    [Fact]
    public async Task Recommendations_ShouldPaginateRankedResults()
    {
        using var factory = CreateFactory();
        factory.Jobs = Enumerable.Range(1, 6)
            .Select(index => factory.Job! with { Id = Guid.NewGuid(), Title = $"Job {index}" })
            .ToArray();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/matching/recommendations?page=2&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<RecommendationApiResponse>();

        Assert.NotNull(result);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal(1, factory.RequestedPage);
        Assert.Equal(100, factory.RequestedPageSize);
        Assert.Equal(1, factory.JobPageCallCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=1000001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Recommendations_ShouldReturn400_WhenPaginationIsInvalid(
        string query)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/recommendations?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.JobPageCallCount);
    }


    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Unknown")]
    public async Task Search_ShouldReturn400_WhenQueryIsInvalid(
        string query)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/matching/results?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await factory.MatchingRepository
            .DidNotReceive()
            .SearchAsync(
                Arg.Any<MatchingResultFilter>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recommendations_ShouldExposeRankingScore_AndRankBeforePagination()
    {
        using var factory = CreateFactory();
        var partial = factory.Job! with { Id = Guid.NewGuid(), Title = "Partial match" };
        var complete = factory.Job! with { Id = Guid.NewGuid(), Title = "Complete match" };
        factory.Jobs = [partial, complete];
        factory.CriteriaByJob[partial.Id] =
        [
            new MatchingCriterion("JobTitle", 100m, 0.5m, "Title matches"),
            MatchingCriterion.NotEvaluated("Skills", 0.5m, "Skills unavailable")
        ];
        factory.CriteriaByJob[complete.Id] =
        [new MatchingCriterion("JobTitle", 90m, 1m, "Title matches")];
        using var client = factory.CreateClient();

        var first = await client.GetFromJsonAsync<RecommendationApiResponse>(
            "/api/v1/matching/recommendations?page=1&pageSize=1");
        var second = await client.GetFromJsonAsync<RecommendationApiResponse>(
            "/api/v1/matching/recommendations?page=2&pageSize=1");

        Assert.NotNull(first);
        Assert.NotNull(second);
        var best = Assert.Single(first.Items);
        var next = Assert.Single(second.Items);
        Assert.Equal(complete.Id, best.JobId);
        Assert.Equal(90m, best.RankingScore);
        Assert.Equal(100m, best.EvaluationCoverage);
        Assert.Equal(partial.Id, next.JobId);
        Assert.Equal(100m, next.OverallScore);
        Assert.Equal(50m, next.RankingScore);
        Assert.Equal(50m, next.EvaluationCoverage);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Recommendations_ShouldReturn503_WhenDependencyFails(bool profileFails, bool timeout)
    {
        using var factory = CreateFactory();
        Exception failure = timeout
            ? new TaskCanceledException("Dependency timeout")
            : new HttpRequestException("Dependency unavailable");
        if (profileFails)
            factory.ProfileClientException = failure;
        else
            factory.JobClientException = failure;
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/matching/recommendations");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.Equal(timeout ? "Matching service dependency timeout" : "Matching service dependency unavailable", problem!.Title);
    }

    private static MatchingApiFactory CreateFactory()
    {
        return new MatchingApiFactory
        {
            Job = new JobMatchingData(
                JobId,
                "Data Engineer",
                "Example Company",
                "Paris",
                "Remote",
                "Permanent",
                50000m,
                65000m,
                "EUR",
                "Python, Spark and Databricks"),
            Profile = new ProfileMatchingData(
                ProfileId,
                "Data Engineer",
                "Experience with Python and Spark",
                [],
                [],
                [],
                [],
                [],
                null)
        };
    }
    private sealed record MatchingResponse(
        Guid Id,
        Guid CandidateProfileId,
        Guid JobId,
        string Status,
        decimal? OverallScore,
        decimal EvaluationCoverage,
        IReadOnlyCollection<MatchingCriterionResponse> Criteria);
    private sealed record MatchingCriterionResponse(
        string Name,
        decimal? Score,
        decimal Weight,
        string Status,
        string Explanation);
}
