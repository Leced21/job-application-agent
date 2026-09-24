using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace JobApplicationAgent.Job.IntegrationTests.Api;
public sealed class JobsControllerTests(JobApiFactory factory) : IClassFixture<JobApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/jobs";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static Dictionary<string, object?> Command() => new()
    {
        ["title"] = " Data Engineer ", ["companyName"] = " Example ", ["location"] = "Paris",
        ["workMode"] = "Hybrid", ["contractType"] = "Permanent", ["salaryMin"] = 50000.25m,
        ["salaryMax"] = 60000m, ["salaryCurrency"] = "EUR", ["description"] = "Description",
        ["source"] = "Manual", ["sourceUrl"] = "https://example.com/job", ["publishedAtUtc"] = "2026-01-01T12:00:00Z"
    };
    [Fact]
    public async Task Crud_ShouldPersistAndPreserveIdentityAndStatus()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(Route, Command());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location!;
        var initial = await client.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal("Data Engineer", initial.GetProperty("title").GetString());
        Assert.Equal("Discovered", initial.GetProperty("status").GetString());
        Assert.Equal(50000.25m, initial.GetProperty("salaryMin").GetDecimal());
        response = await client.PatchAsJsonAsync(location + "/status", new { status = "Saved" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Command(); command["title"] = "Architect"; command["salaryMin"] = null; command["salaryMax"] = null; command["salaryCurrency"] = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(location, command)).StatusCode);
        var updated = await client.GetFromJsonAsync<JsonElement>(location);
        Assert.Equal("Saved", updated.GetProperty("status").GetString());
        Assert.Equal("Architect", updated.GetProperty("title").GetString());
        Assert.Equal(initial.GetProperty("createdAtUtc").GetString(), updated.GetProperty("createdAtUtc").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("salaryMin").ValueKind);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await db.Jobs.CountAsync());
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(location)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(Route))!);
    }
    [Theory]
    [InlineData("title", " ")]
    [InlineData("workMode", "Invalid")]
    [InlineData("contractType", "Invalid")]
    [InlineData("sourceUrl", "javascript:alert(1)")]
    [InlineData("salaryCurrency", "")]
    public async Task InvalidCreate_ShouldNotPersist(string key, string value)
    {
        var client = factory.CreateClient(); var command = Command(); command[key] = value;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, command)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>(Route))!);
    }
    [Fact]
    public async Task InvalidUpdates_ShouldPreserveStoredOffer()
    {
        var client = factory.CreateClient(); var created = await client.PostAsJsonAsync(Route, Command());
        var location = created.Headers.Location!; var before = await client.GetStringAsync(location);
        var command = Command(); command["salaryMin"] = 70000;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(location, command)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(location + "/status", new { status = 99 })).StatusCode);
        Assert.Equal(before, await client.GetStringAsync(location));
    }
    [Fact]
    public async Task MissingOffer_ShouldReturnNotFound()
    {
        var client = factory.CreateClient(); var route = Route + "/" + Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(route, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PatchAsJsonAsync(route + "/status", new { status = "Saved" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(route)).StatusCode);
    }
    [Fact]
    public async Task Pagination_ShouldReturnDistinctPagesAndRejectInvalidBounds()
    {
        var client = factory.CreateClient();
        for (var n = 0; n < 3; n++) Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Route, Command())).StatusCode);
        var first = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=1&pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<JsonElement[]>(Route + "?page=2&pageSize=2"))!;
        Assert.Equal(2, first.Length); Assert.Single(second);
        Assert.DoesNotContain(first, x => x.GetProperty("id").GetString() == second[0].GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?pageSize=101")).StatusCode);
    }
}
