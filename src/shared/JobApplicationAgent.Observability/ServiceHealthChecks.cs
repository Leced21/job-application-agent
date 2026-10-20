using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JobApplicationAgent.Observability;

public sealed class PostgresReadinessCheck<TContext>(IServiceScopeFactory scopeFactory) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TContext>();
        return await database.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Database connection available.")
            : HealthCheckResult.Unhealthy("Database is unavailable.");
    }
}

public static class ServiceHealthChecks
{
    public static IHealthChecksBuilder AddPostgresReadiness<TContext>(this IHealthChecksBuilder checks, string name = "postgres")
        where TContext : DbContext => checks.AddCheck<PostgresReadinessCheck<TContext>>(name, tags: ["ready"]);

    public static WebApplication MapServiceHealthChecks(this WebApplication app)
    {
        var readiness = new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") };
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", readiness);
        app.MapHealthChecks("/health", readiness);
        return app;
    }
}
