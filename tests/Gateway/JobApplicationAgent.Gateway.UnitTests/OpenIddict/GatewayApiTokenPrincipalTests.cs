using JobApplicationAgent.Gateway.OpenIddict;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JobApplicationAgent.Gateway.UnitTests.OpenIddict;

public sealed class GatewayApiTokenPrincipalTests
{
    [Fact]
    public void Create_ContainsOnlyTheOwnerAndApiScope()
    {
        var ownerId = Guid.NewGuid();

        var principal = GatewayApiTokenPrincipal.Create("account-subject", ownerId);

        Assert.Equal("account-subject", principal.GetClaim(Claims.Subject));
        Assert.Equal(ownerId.ToString("D"), principal.GetClaim("jobagent_owner_id"));
        Assert.Equal("jobagent_api", Assert.Single(principal.GetScopes()));
        Assert.Equal("jobagent_api", Assert.Single(principal.GetResources()));
        Assert.All(principal.Claims.Where(claim => claim.Type is Claims.Subject or "jobagent_owner_id"),
            claim => Assert.Equal(Destinations.AccessToken, Assert.Single(claim.GetDestinations())));
    }

    [Fact]
    public void Create_RejectsMissingIdentity()
    {
        Assert.Throws<ArgumentException>(() => GatewayApiTokenPrincipal.Create(" ", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => GatewayApiTokenPrincipal.Create("account", Guid.Empty));
    }
}
