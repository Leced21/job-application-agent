
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Matching.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<CalculateMatchingHandler>();

        services.AddScoped<IMatchingScoringEngine, StructuredMatchingScoringEngine>();

        return services;
    }
}
