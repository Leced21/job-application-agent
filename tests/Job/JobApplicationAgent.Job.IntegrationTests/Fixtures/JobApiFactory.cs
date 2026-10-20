using JobApplicationAgent.Job.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Job.IntegrationTests.Infrastructure;

public sealed class JobApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid TestOwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public const string LocalToken = "job-integration-local-admin-token-with-at-least-32-characters";
    public const string CollectionPublisherToken = "job-integration-collection-publisher-token-with-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("job_test_db").WithUsername("test_" + Guid.NewGuid().ToString("N")[..8])
        .WithPassword(Guid.NewGuid().ToString("N")).Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:JobDatabase"] = _postgres.GetConnectionString(),
                ["Job:LocalAuth:OwnerId"] = TestOwnerId.ToString(), ["Job:LocalAuth:Token"] = LocalToken,
                ["Job:Admin:OwnerIds"] = TestOwnerId.ToString(), ["Job:CollectionPublisher:Token"] = CollectionPublisherToken
            }));
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", LocalToken);
        return client;
    }
    public HttpClient CreateUnauthenticatedClient() => base.CreateClient();
    public HttpClient CreatePublisherClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CollectionPublisherToken);
        return client;
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<JobDbContext>().Database.MigrateAsync();
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.Jobs.ExecuteDeleteAsync();
        await db.CollectionPublications.ExecuteDeleteAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
