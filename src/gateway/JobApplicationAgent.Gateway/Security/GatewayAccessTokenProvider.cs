using System.Security.Claims;
using JobApplicationAgent.Gateway.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using static OpenIddict.Client.OpenIddictClientModels;

namespace JobApplicationAgent.Gateway.Security;

public sealed class GatewayAccessTokenProvider(
    IHttpContextAccessor contextAccessor,
    UserManager<ApplicationUser> users,
    OpenIddictClientService client,
    IMemoryCache cache,
    IConfiguration configuration,
    ILogger<GatewayAccessTokenProvider> logger)
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext;
        var accountId = context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var clientId = configuration["OpenIddict:Bff:ClientId"];
        var issuer = configuration["OpenIddict:Issuer"];
        if (!Guid.TryParse(accountId, out var parsedAccountId) || string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(configuration["OpenIddict:Bff:ClientSecret"]) ||
            !Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
            return null;

        var user = await users.FindByIdAsync(parsedAccountId.ToString());
        if (user?.OwnerId is not { } ownerId || ownerId == Guid.Empty || await users.IsLockedOutAsync(user))
            return null;

        var cacheKey = $"gateway-api-token:{ownerId:D}";
        if (cache.TryGetValue(cacheKey, out string? cachedToken))
            return cachedToken;

        var token = await client.AuthenticateWithClientCredentialsAsync(new ClientCredentialsAuthenticationRequest
        {
            Issuer = issuerUri,
            CancellationToken = cancellationToken,
            AdditionalTokenRequestParameters = new Dictionary<string, OpenIddictParameter>
            {
                ["jobagent_owner_id"] = new(ownerId.ToString("D"))
            },
            Scopes = new List<string> { "jobagent_api" }
        });
        if (string.IsNullOrWhiteSpace(token.AccessToken) || token.AccessTokenExpirationDate is not { } expiresAt ||
            expiresAt <= DateTimeOffset.UtcNow.AddSeconds(10))
        {
            logger.LogWarning("OpenIddict did not issue a usable Gateway API token.");
            return null;
        }

        cache.Set(cacheKey, token.AccessToken, expiresAt.AddSeconds(-10));
        return token.AccessToken;
    }
}
