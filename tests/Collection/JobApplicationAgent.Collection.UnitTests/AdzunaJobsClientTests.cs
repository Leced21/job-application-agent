using System.Net;
using JobApplicationAgent.Collection.Application.Connectors;
using JobApplicationAgent.Collection.Infrastructure.Connectors;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class AdzunaJobsClientTests
{
    [Fact]
    public async Task SearchAsync_MapsOfferFieldsAndUsesSecureAdzunaAttributionUrl()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "results": [{
                "title": "Développeur .NET",
                "company": { "display_name": "Exemple SAS" },
                "location": { "display_name": "Lyon, Rhône" },
                "description": "Extrait fourni par Adzuna",
                "redirect_url": "http://www.adzuna.fr/jobs/land/ad/123?utm_source=test",
                "created": "2026-10-19T10:15:00Z",
                "contract_type": "permanent",
                "salary_min": 50000,
                "salary_max": 65000
              }]
            }
            """)
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.adzuna.com/v1/api/") };
        var client = new AdzunaJobsClient(http, Options.Create(ConfiguredOptions()));

        var offer = Assert.Single(await client.SearchAsync(".NET developer", "Lyon", 2, default));

        Assert.Equal("Développeur .NET", offer.Title);
        Assert.Equal("Exemple SAS", offer.CompanyName);
        Assert.Equal("Lyon, Rhône", offer.Location);
        Assert.Equal("Unknown", offer.WorkMode);
        Assert.Equal("Permanent", offer.ContractType);
        Assert.Equal("Extrait fourni par Adzuna", offer.Description);
        Assert.Equal("Adzuna", offer.SourceName);
        Assert.Equal("https://www.adzuna.fr/jobs/land/ad/123?utm_source=test", offer.SourceUrl);
        Assert.Equal(new DateTime(2026, 10, 19, 10, 15, 0, DateTimeKind.Utc), offer.PublishedAtUtc);
        Assert.Null(offer.SalaryMin);
        Assert.Null(offer.Skills);
        Assert.Equal("fr/", handler.LastRequest!.RequestUri!.Segments[4]);
        Assert.Contains("sort_by=date", handler.LastRequest.RequestUri.Query);
        Assert.Contains("what=.NET%20developer", handler.LastRequest.RequestUri.Query);
        Assert.Contains("where=Lyon", handler.LastRequest.RequestUri.Query);
        Assert.Contains("app_key=private-test-key", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task SearchAsync_DropsOffersWithoutRequiredFieldsOrAdzunaRedirects()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "results": [
              { "title": "Sans entreprise", "redirect_url": "https://www.adzuna.fr/details/1" },
              { "title": "Lien externe", "company": { "display_name": "Entreprise" }, "redirect_url": "https://example.com/job/2" }
            ] }
            """)
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.adzuna.com/v1/api/") };
        var client = new AdzunaJobsClient(http, Options.Create(ConfiguredOptions()));

        Assert.Empty(await client.SearchAsync("developer", null, 1, default));
    }

    [Fact]
    public async Task SearchAsync_WithoutCredentialsFailsWithoutRevealingAnySecret()
    {
        using var http = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("Should not send a request.")))
        { BaseAddress = new Uri("https://api.adzuna.com/v1/api/") };
        var client = new AdzunaJobsClient(http, Options.Create(new AdzunaOptions { AppKey = "do-not-print-this" }));

        var error = await Assert.ThrowsAsync<CollectionSourceUnavailableException>(
            () => client.SearchAsync("developer", null, 1, default));

        Assert.DoesNotContain("do-not-print-this", error.Message);
    }

    [Fact]
    public async Task SearchAsync_WhenAdzunaRejectsRequestReturnsGenericUnavailableError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.adzuna.com/v1/api/") };
        var client = new AdzunaJobsClient(http, Options.Create(ConfiguredOptions()));

        var error = await Assert.ThrowsAsync<CollectionSourceUnavailableException>(
            () => client.SearchAsync("developer", null, 1, default));

        Assert.DoesNotContain("private-test-key", error.Message);
    }

    private static AdzunaOptions ConfiguredOptions() => new()
    {
        AppId = "test-id",
        AppKey = "private-test-key",
        Country = "fr",
        ResultsPerPage = 20
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }
}
