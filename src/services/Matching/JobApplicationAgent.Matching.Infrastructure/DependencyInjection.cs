
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using JobApplicationAgent.Matching.Infrastructure.Persistence;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Matching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<MatchingUserTokenForwarder>();
        services.AddHttpClient<IJobMatchingDataClient, JobMatchingHttpClient>(
            (serviceProvider, client) =>
            {
                var jobServiceUrl = serviceProvider.GetRequiredService<IConfiguration>()["Services:Job:BaseUrl"]
                    ?? throw new InvalidOperationException("Services:Job:BaseUrl is not configured.");
                client.BaseAddress = new Uri(jobServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
                client.MaxResponseContentBufferSize = 8 * 1024 * 1024;
            });

        services.AddHttpClient<IProfileMatchingDataClient, ProfileMatchingHttpClient>(
            (serviceProvider, client) =>
            {
                var profileServiceUrl = serviceProvider.GetRequiredService<IConfiguration>()["Services:Profile:BaseUrl"]
                    ?? throw new InvalidOperationException("Services:Profile:BaseUrl is not configured.");
                client.BaseAddress = new Uri(profileServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            }).AddHttpMessageHandler<MatchingUserTokenForwarder>();
        var connectionString = configuration.GetConnectionString("MatchingDb")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:MatchingDb is not configured.");

        services.AddDbContext<MatchingDbContext>(options =>options.UseNpgsql(connectionString));
        services.AddScoped<IMatchingResultRepository, MatchingResultRepository>();
        services.AddScoped<IJobPublishedEventInbox, JobPublishedEventInbox>();
        services.AddScoped<JobApplicationAgent.Matching.Application.Events.JobPublishedEventProcessor>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
