
using JobApplicationAgent.Matching.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class MatchingPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("matching_test_db")
            .WithUsername("jobagent")
            .WithPassword("jobagent_test")
            .Build();

    private ServiceProvider? _serviceProvider;

    public MatchingDbContext CreateDbContext()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                "PostgreSQL test fixture has not been initialized.");
        }

        return _serviceProvider
            .GetRequiredService<IDbContextFactory<MatchingDbContext>>()
            .CreateDbContext();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();

        services.AddDbContextFactory<MatchingDbContext>(options =>
            options.UseNpgsql(_container.GetConnectionString()));

        _serviceProvider = services.BuildServiceProvider();

        await using var dbContext = CreateDbContext();

        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
