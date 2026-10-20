using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobApplicationAgent.Collection.Infrastructure.Events;

public sealed class CollectionOutboxDispatchWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<CollectionOutboxDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Collection:EventTransport:Enabled")) return;
        var failures = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<CollectionOutboxDispatchService>();
                var attempt = await dispatcher.DispatchOneAsync(stoppingToken);
                if (attempt is null) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                else if (attempt.Published) failures = 0;
                else logger.LogWarning("Collection event {EventId} publish failed ({FailureCode}); retry is scheduled for {RetryAtUtc}.",
                    attempt.EventId, attempt.FailureCode, attempt.RetryAtUtc);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                failures = Math.Min(failures + 1, 6);
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, failures)));
                logger.LogWarning("Collection outbox relay failed ({FailureType}); retrying after {DelaySeconds}s.",
                    exception.GetType().Name, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
