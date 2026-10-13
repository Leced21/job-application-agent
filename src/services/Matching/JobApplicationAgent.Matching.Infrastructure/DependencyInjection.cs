
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Infrastructure.HttpClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Matching.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jobServiceUrl = configuration["Services:Job:BaseUrl"]
            ?? throw new InvalidOperationException("Services:Job:BaseUrl is not configured.");

        var profileServiceUrl = configuration["Services:Profile:BaseUrl"]
            ?? throw new InvalidOperationException("Services:Profile:BaseUrl is not configured.");

        services.AddHttpClient<IJobMatchingDataClient, JobMatchingHttpClient>(
            client =>
            {
                client.BaseAddress = new Uri(jobServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        services.AddHttpClient<IProfileMatchingDataClient, ProfileMatchingHttpClient>(
            client =>
            {
                client.BaseAddress = new Uri(profileServiceUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        return services;
    }
}
