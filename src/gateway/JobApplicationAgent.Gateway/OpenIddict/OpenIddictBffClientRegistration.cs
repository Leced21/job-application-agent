using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JobApplicationAgent.Gateway.OpenIddict;

public static class OpenIddictBffClientRegistration
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration configuration)
    {
        var clientId = configuration["OpenIddict:Bff:ClientId"];
        var clientSecret = configuration["OpenIddict:Bff:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) && string.IsNullOrWhiteSpace(clientSecret))
            return;
        if (string.IsNullOrWhiteSpace(clientId) || clientSecret is null || clientSecret.Length < 32)
            throw new InvalidOperationException("Configure both OpenIddict:Bff:ClientId and a 32+ character OpenIddict:Bff:ClientSecret.");

        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            ClientType = ClientTypes.Confidential,
            DisplayName = "Job Application Agent Gateway"
        };
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(Permissions.Prefixes.Scope + "jobagent_api");

        var application = await manager.FindByClientIdAsync(clientId);
        if (application is null)
            await manager.CreateAsync(descriptor);
        else
            await manager.UpdateAsync(application, descriptor);
    }
}
