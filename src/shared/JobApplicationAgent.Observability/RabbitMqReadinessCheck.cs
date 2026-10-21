using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace JobApplicationAgent.Observability;

public sealed class RabbitMqReadinessCheck(IConfiguration configuration, string enabledKey, string uriKey, string clientName)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>(enabledKey))
            return HealthCheckResult.Healthy("RabbitMQ transport is disabled.");

        var value = configuration[uriKey];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("amqp" or "amqps") ||
            string.IsNullOrWhiteSpace(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.IsLoopback || string.Equals(uri.Host, "rabbitmq", StringComparison.OrdinalIgnoreCase)))
            return HealthCheckResult.Unhealthy("RabbitMQ configuration is unavailable.");

        try
        {
            var factory = new ConnectionFactory { Uri = uri, ClientProvidedName = clientName };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ connection available.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection timed out.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("RabbitMQ is unavailable.");
        }
    }
}

public static class RabbitMqHealthCheckRegistration
{
    public static IHealthChecksBuilder AddRabbitMqReadiness(this IHealthChecksBuilder checks, IConfiguration configuration,
        string enabledKey, string uriKey, string clientName)
    {
        checks.Add(new HealthCheckRegistration("rabbitmq", _ =>
            new RabbitMqReadinessCheck(configuration, enabledKey, uriKey, clientName),
            failureStatus: HealthStatus.Unhealthy, tags: ["ready"], timeout: TimeSpan.FromSeconds(5)));
        return checks;
    }
}
