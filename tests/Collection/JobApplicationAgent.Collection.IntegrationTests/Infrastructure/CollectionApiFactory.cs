using JobApplicationAgent.Collection.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Collection.IntegrationTests.Infrastructure;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class CollectionApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid TestOwnerId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public const string ServiceToken = "collection-integration-test-token-with-at-least-32-characters";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("collection_tests").WithUsername("test_" + Guid.NewGuid().ToString("N")[..8])
        .WithPassword(Guid.NewGuid().ToString("N")).Build();
    public TestClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CollectionDatabase"] = _postgres.GetConnectionString(),
                ["Collection:LocalAuth:OwnerId"] = TestOwnerId.ToString(), ["Collection:LocalAuth:Token"] = ServiceToken,
                ["Collection:Admin:OwnerIds"] = TestOwnerId.ToString(),
                ["Job:CollectionPublisher:Token"] = "job-integration-collection-publisher-token-with-at-least-32-characters"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ServiceToken);
        return client;
    }
    public HttpClient CreateUnauthenticatedClient() => base.CreateClient();
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CollectionDbContext>().Database.MigrateAsync();
    }
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CollectionDbContext>().Offers.ExecuteDeleteAsync();
        await scope.ServiceProvider.GetRequiredService<CollectionDbContext>().OutboxMessages.ExecuteDeleteAsync();
        Clock.Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
