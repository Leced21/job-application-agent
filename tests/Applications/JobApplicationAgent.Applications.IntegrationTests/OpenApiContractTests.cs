using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace JobApplicationAgent.Applications.IntegrationTests;

public sealed class OpenApiContractTests(ApplicationsApiFactory factory) : IClassFixture<ApplicationsApiFactory>
{
    [Fact]
    public async Task Development_exposes_application_routes_in_openapi_document()
    {
        using var developmentHost = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = developmentHost.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/applications", out _));
    }
}
