using JobApplicationAgent.Profile.Api;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Profile.IntegrationTests.Api;

public sealed class ProfileServiceIdentityTests
{
    private static readonly Guid OwnerId = Guid.Parse("48c6a244-09d4-405f-b46e-74655149a352");
    private const string Token = "profile-local-transition-token-with-at-least-32-characters";

    [Fact]
    public void From_ShouldDisableStaticIdentity_WhenLocalAuthenticationIsNotAllowed()
    {
        var identity = ProfileServiceIdentity.From(Configuration(), allowLocalAuth: false);

        Assert.False(identity.Enabled);
        Assert.Empty(identity.TokenHash);
        Assert.Equal(Guid.Empty, identity.OwnerId);
    }

    [Fact]
    public void From_ShouldLoadStaticIdentity_WhenLocalAuthenticationIsAllowed()
    {
        var identity = ProfileServiceIdentity.From(Configuration(), allowLocalAuth: true);

        Assert.True(identity.Enabled);
        Assert.Equal(OwnerId, identity.OwnerId);
        Assert.Equal(32, identity.TokenHash.Length);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Profile:LocalAuth:Token"] = Token,
            ["Profile:LocalAuth:OwnerId"] = OwnerId.ToString()
        })
        .Build();
}
