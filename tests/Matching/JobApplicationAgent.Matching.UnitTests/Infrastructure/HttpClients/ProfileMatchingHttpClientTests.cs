
using System.Net;
using System.Text;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Infrastructure.HttpClients;

public sealed class ProfileMatchingHttpClientTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnProfile_WhenResponseIsSuccessful()
    {
        const string json = """
        {
          "candidateProfileId": "11111111-1111-1111-1111-111111111111",
          "jobTitle": "Data Engineer",
          "summary": "Data engineering with Spark and Python",
          "experiences": [],
          "skills": [
            {
              "name": "Python",
              "category": "Programming",
              "level": "Advanced",
              "yearsOfExperience": 4
            }
          ],
          "educations": [],
          "certifications": [],
          "languages": [],
          "preferences": null
        }
        """;

        var client = CreateClient(HttpStatusCode.OK, json);

        var result = await client.GetAsync();

        Assert.NotNull(result);
        Assert.Equal("Data Engineer", result.JobTitle);
        Assert.Single(result.Skills);
        Assert.Equal("Python", result.Skills.Single().Name);
        Assert.Equal(4, result.Skills.Single().YearsOfExperience);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenProfileDoesNotExist()
    {
        var client = CreateClient(HttpStatusCode.NotFound);

        var result = await client.GetAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ShouldThrow_WhenServerReturnsError()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync());
    }

    [Fact]
    public async Task GetAsync_ShouldCallExpectedEndpoint()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound);

        var client = new ProfileMatchingHttpClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5001/")
            });

        await client.GetAsync();

        Assert.Equal(
            "/api/v1/profile/matching-data",
            handler.RequestPath);
    }

    private static ProfileMatchingHttpClient CreateClient(
        HttpStatusCode statusCode,
        string? json = null)
    {
        var handler = new StubHttpMessageHandler(statusCode, json);

        return new ProfileMatchingHttpClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:5001/")
            });
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
