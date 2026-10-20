using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Infrastructure.Persistence;
using JobApplicationAgent.Collection.Infrastructure.Repositories;
using JobApplicationAgent.Collection.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Collection.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<CollectionDbContext>((provider, options) => options.UseNpgsql(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("CollectionDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings:CollectionDatabase is required.")));
        services.AddScoped<ICollectedOfferRepository, CollectedOfferRepository>();
        services.AddScoped<JobApplicationAgent.Collection.Application.Connectors.ICollectedOfferBatchRepository, CollectedOfferRepository>();
        services.AddSingleton<JobApplicationAgent.Collection.Application.Connectors.IOfferSourceConnector, Connectors.LocalJsonExportConnector>();
        services.AddOptions<Connectors.AdzunaOptions>().BindConfiguration("Collection:Adzuna");
        services.AddHttpClient<Connectors.IAdzunaJobsClient, Connectors.AdzunaJobsClient>("Adzuna", (provider, http) =>
        {
            http.BaseAddress = new Uri("https://api.adzuna.com/v1/api/");
            http.Timeout = TimeSpan.FromSeconds(15);
            http.MaxResponseContentBufferSize = 1_048_576;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<JobApplicationAgent.Collection.Application.Connectors.IOfferSourceConnector, Connectors.AdzunaOfferSourceConnector>();
        services.AddScoped<IOfferRevisionRepository, OfferRevisionRepository>();
        services.AddScoped<CollectionOutboxDispatchService>();
        services.AddSingleton<JobApplicationAgent.Collection.Application.Events.ICollectionEventPublisher, RabbitMqCollectionEventPublisher>();
        services.AddHttpClient<JobApplicationAgent.Collection.Application.Imports.IJobPublicationClient, JobPublicationClient>((provider, http) =>
        {
            var url = provider.GetRequiredService<IConfiguration>()["Services:Job:BaseUrl"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("Services:Job:BaseUrl must be an HTTP(S) service URL.");
            http.BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(10);
            http.MaxResponseContentBufferSize = 65536;
            var publisherToken = provider.GetRequiredService<IConfiguration>()["Job:CollectionPublisher:Token"];
            if (publisherToken is null || publisherToken.Length is < 32 or > 512 || publisherToken.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("Configure a distinct strong Job:CollectionPublisher:Token (32–512 characters) outside Git.");
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", publisherToken);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        return services;
    }
}
