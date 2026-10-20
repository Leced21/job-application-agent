extern alias JobReceiver;
using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Infrastructure;
using JobApplicationAgent.Collection.IntegrationTests.Infrastructure;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class JobReceiverFactory : WebApplicationFactory<JobReceiver::Program>, IAsyncLifetime
{
    private const string LocalToken = "job-integration-local-admin-token-with-at-least-32-characters";
    public const string CollectionPublisherToken = "job-integration-collection-publisher-token-with-at-least-32-characters";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("receiver_tests").WithUsername("test_" + Guid.NewGuid().ToString("N")[..8])
        .WithPassword(Guid.NewGuid().ToString("N")).Build();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:JobDatabase"] = postgres.GetConnectionString(),
            ["Job:LocalAuth:OwnerId"] = OwnerId.ToString(), ["Job:LocalAuth:Token"] = LocalToken,
            ["Job:Admin:OwnerIds"] = OwnerId.ToString(), ["Job:CollectionPublisher:Token"] = CollectionPublisherToken
        }));
    }
    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", LocalToken);
        return client;
    }
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<JobDbContext>().Database.MigrateAsync();
    }
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        await db.Jobs.ExecuteDeleteAsync();
        await db.CollectionPublications.ExecuteDeleteAsync();
    }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
}

public sealed class PublicationFlowTests(CollectionApiFactory collection, JobReceiverFactory receiver)
    : IClassFixture<CollectionApiFactory>, IClassFixture<JobReceiverFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/collection/imports";
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() { await collection.ResetAsync(); await receiver.ResetAsync(); }

    private sealed class LoseFirstPostResponse(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private int remaining = 1;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (request.Method == HttpMethod.Post && Interlocked.Exchange(ref remaining, 0) == 1)
            {
                response.Dispose();
                throw new HttpRequestException("Simulated response loss after receiver commit.");
            }
            return response;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovedPublication_ShouldSurviveConcurrencyResponseLossAndDeletion(bool loseResponse)
    {
        using var transport = loseResponse
            ? new HttpClient(new LoseFirstPostResponse(receiver.Server.CreateHandler())) { BaseAddress = new("http://localhost") }
            : receiver.CreateClient();
        transport.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", JobReceiverFactory.CollectionPublisherToken);
        using var host = collection.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IJobPublicationClient>();
            services.AddSingleton<IJobPublicationClient>(new JobPublicationClient(transport));
        }));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CollectionApiFactory.ServiceToken);
        collection.Clock.Now = DateTimeOffset.UtcNow;
        var command = new ImportOfferCommand("Engineer", "Company", WorkMode: "Remote", ContractType: "CDI",
            Skills: ["SQL"], SourceUrl: "https://example.invalid/1", ExpiresAtUtc: collection.Clock.Now.AddHours(1).UtcDateTime);
        var imported = await client.PostAsJsonAsync(Route, command);
        imported.EnsureSuccessStatusCode();
        var id = (await imported.Content.ReadFromJsonAsync<ImportOfferResponse>())!.Offer.Id;
        var revision = Assert.Single((await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!);
        var path = $"{Route}/{id}/revisions/{revision.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/publish", null)).StatusCode);
        (await client.PostAsJsonAsync(path + "/review", new ReviewOfferCommand("Approved"))).EnsureSuccessStatusCode();
        if (loseResponse) Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync(path + "/publish", null)).StatusCode);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsync(path + "/publish", null)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var receipts = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<PublicationReceipt>()));
        var jobId = Assert.Single(receipts.Select(receipt => receipt!.JobId).Distinct());
        foreach (var response in responses) response.Dispose();
        using (var scope = receiver.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
            var job = Assert.Single(await db.Jobs.ToArrayAsync());
            Assert.Equal(jobId, job.Id);
            Assert.Equal("Engineer", job.Title);
            Assert.Equal("Remote", job.WorkMode.ToString());
            Assert.Equal("Permanent", job.ContractType.ToString());
            Assert.Contains("SQL", (await db.CollectionPublications.SingleAsync()).RequestJson);
        }
        // A distinct approved revision must not silently overwrite or create another Job.
        (await client.PostAsJsonAsync(Route, command with { Title = "Changed" })).EnsureSuccessStatusCode();
        var changed = (await client.GetFromJsonAsync<OfferRevisionDto[]>($"{Route}/{id}/revisions"))!.Single(x => x.Content.Title == "Changed");
        (await client.PostAsJsonAsync($"{Route}/{id}/revisions/{changed.Id}/review", new ReviewOfferCommand("Approved"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"{Route}/{id}/revisions/{changed.Id}/publish", null)).StatusCode);
        using var jobClient = receiver.CreateClient();
        (await jobClient.DeleteAsync($"/api/v1/jobs/{jobId}")).EnsureSuccessStatusCode();
        collection.Clock.Now = collection.Clock.Now.AddHours(2);
        var retry = await client.PostAsync(path + "/publish", null);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(jobId, (await retry.Content.ReadFromJsonAsync<PublicationReceipt>())!.JobId);
        Assert.Equal(HttpStatusCode.NotFound, (await jobClient.GetAsync($"/api/v1/jobs/{jobId}")).StatusCode);
    }
}
