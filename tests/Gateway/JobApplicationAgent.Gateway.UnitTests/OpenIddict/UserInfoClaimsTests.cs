using System.Security.Claims;
using JobApplicationAgent.Gateway.OpenIddict;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JobApplicationAgent.Gateway.UnitTests.OpenIddict;

public sealed class UserInfoClaimsTests
{
    [Fact]
    public void Create_OnlyReturnsClaimsCoveredByGrantedScopes()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        principal.SetScopes(Scopes.Profile);
        principal.Identities.Single().AddClaim(new Claim("jobagent_owner_id", "owner-456"));

        var response = UserInfoClaims.Create(principal, "account-123", "candidate@example.test",
            "candidate@example.test", emailConfirmed: true, ["Administrator"]);

        Assert.Equal("account-123", response[Claims.Subject]);
        Assert.Equal("candidate@example.test", response[Claims.Name]);
        Assert.DoesNotContain(Claims.Email, response.Keys);
        Assert.DoesNotContain(Claims.Role, response.Keys);
        Assert.DoesNotContain("jobagent_owner_id", response.Keys);
    }

    [Fact]
    public void Create_ReturnsEmailVerificationAndRolesWhenScopesAreGranted()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        principal.SetScopes(Scopes.Email, Scopes.Roles);

        var response = UserInfoClaims.Create(principal, "account-123", "candidate",
            "candidate@example.test", emailConfirmed: false, ["Candidate"]);

        Assert.Equal("candidate@example.test", response[Claims.Email]);
        Assert.Equal(false, response[Claims.EmailVerified]);
        Assert.Equal(new[] { "Candidate" }, response[Claims.Role]);
        Assert.DoesNotContain(Claims.Name, response.Keys);
    }
}
