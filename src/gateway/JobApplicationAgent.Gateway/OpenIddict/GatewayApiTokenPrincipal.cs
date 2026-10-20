using System.Security.Claims;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JobApplicationAgent.Gateway.OpenIddict;

public static class GatewayApiTokenPrincipal
{
    public static ClaimsPrincipal Create(string subject, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(subject) || ownerId == Guid.Empty)
            throw new ArgumentException("A subject and owner identifier are required.");

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new(Claims.Subject, subject),
                new("jobagent_owner_id", ownerId.ToString("D"))
            }, "OpenIddict"));
        principal.SetScopes("jobagent_api");
        principal.SetResources("jobagent_api");
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject or "jobagent_owner_id" => [Destinations.AccessToken],
            _ => []
        });
        return principal;
    }
}
