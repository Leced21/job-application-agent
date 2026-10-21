using FluentValidation;
using JobApplicationAgent.Collection.Application.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobApplicationAgent.Collection.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ImportOfferCommandValidator>();
        services.AddScoped<ImportOfferHandler>();
        services.AddScoped<GetImportedOffersHandler>();
        services.AddScoped<ReviewOfferHandler>();
        services.AddScoped<PublishOfferHandler>();
        services.AddScoped<JobApplicationAgent.Collection.Application.Connectors.CollectSourceHandler>();
        services.AddSingleton<OfferNormalizer>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
