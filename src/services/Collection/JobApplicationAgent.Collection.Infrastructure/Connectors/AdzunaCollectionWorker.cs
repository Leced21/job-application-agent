using JobApplicationAgent.Collection.Application.Connectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Collection.Infrastructure.Connectors;

public sealed class AdzunaCollectionWorker(
    IServiceScopeFactory scopes,
    IOptions<AdzunaOptions> settings,
    ILogger<AdzunaCollectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = settings.Value;
        if (!options.Sync.Enabled) return;

        var searches = options.Sync.Searches
            .Where(search => !string.IsNullOrWhiteSpace(search.What))
            .Take(11)
            .ToArray();
        if (string.IsNullOrWhiteSpace(options.AppId) || string.IsNullOrWhiteSpace(options.AppKey) ||
            searches.Length is < 1 or > 10 || options.Sync.IntervalMinutes is < 60 or > 1440)
            throw new InvalidOperationException("Enabled Adzuna sync requires credentials, 1–10 searches and an interval from 60 to 1440 minutes.");

        logger.LogInformation("Adzuna collection is enabled with {SearchCount} configured searches.", searches.Length);
        while (!stoppingToken.IsCancellationRequested)
        {
            for (var index = 0; index < searches.Length; index++)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    using var scope = scopes.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<CollectSourceHandler>();
                    var search = searches[index];
                    var result = await handler.HandleAsync("adzuna",
                        new SourceCollectionRequest(null, search.What, search.Where), stoppingToken);
                    logger.LogInformation("Adzuna collection search {SearchIndex} received {Received} offers ({Created} new, {Duplicates} duplicates, {Changed} changed).",
                        index + 1, result.Received, result.Created, result.Duplicates, result.Changed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Search terms and API request URIs are intentionally excluded from logs.
                    logger.LogWarning("Adzuna collection search {SearchIndex} failed ({ExceptionType}).",
                        index + 1, exception.GetType().Name);
                }

                if (index + 1 < searches.Length)
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(options.Sync.IntervalMinutes), stoppingToken);
        }
    }
}
