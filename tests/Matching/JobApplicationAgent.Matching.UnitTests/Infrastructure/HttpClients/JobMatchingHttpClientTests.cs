
using System.Net;
using System.Text;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Infrastructure.HttpClients;

public sealed class JobMatchingHttpClientTests
{
    private static readonly Guid JobId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetByIdAsync_ShouldReturnJob_WhenResponseIsSuccessful()
    {
        const string json = """
        {
          "id": "11111111-1111-1111-1111-111111111111",
          "title": "Data Engineer",
          "companyName": "Example Company",
          "location": "Paris",
          "workMode": "Remote",
          "contractType": "Permanent",
          "salaryMin": 50000,
          "salaryMax": 65000,
          "salaryCurrency": "EUR",
          "description": "Python, Spark and Databricks"
        }
        """;

        var client = CreateClient(
            HttpStatusCode.OK,
            json);

        var result = await client.GetByIdAsync(JobId);

        Assert.NotNull(result);
        Assert.Equal(JobId, result.Id);
        Assert.Equal("Data Engineer", result.Title);
        Assert.Equal("Example Company", result.CompanyName);
        Assert.Equal("Remote", result.WorkMode);
        Assert.Equal("Permanent", result.ContractType);
        Assert.Equal(50000m, result.SalaryMin);
        Assert.Equal(65000m, result.SalaryMax);
        Assert.Contains("Databricks", result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var client = CreateClient(HttpStatusCode.NotFound);

        var result = await client.GetByIdAsync(JobId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrow_WhenServerReturnsInternalError()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetByIdAsync(JobId));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldRejectEmptyJobId()
    {
        var client = CreateClient(HttpStatusCode.OK);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldCallExpectedEndpoint()
    {
        var handler = new StubHttpMessageHandler(
            HttpStatusCode.NotFound);

        var client = new JobMatchingHttpClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5002/")
            });

        await client.GetByIdAsync(JobId);

        Assert.Equal(
            $"/api/v1/jobs/{JobId}",
            handler.RequestPath);
    }

    private static JobMatchingHttpClient CreateClient(
        HttpStatusCode statusCode,
        string? json = null)
    {
        var handler = new StubHttpMessageHandler(
            statusCode,
            json);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5002/")
        };

        return new JobMatchingHttpClient(httpClient);
    }

    private sealed class StubHttpMessageHandler(
        HttpStatusCode statusCode,
        string? json = null) : HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.AbsolutePath;

            var response = new HttpResponseMessage(statusCode);

            if (json is not null)
            {
                response.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
