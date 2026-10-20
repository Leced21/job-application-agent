using JobApplicationAgent.Agents.Api;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.IntegrationTests;

public sealed class LocalIdentityTests
{
    [Fact]
    public void From_ShouldDisableStaticIdentity_WhenLocalAuthenticationIsNotAllowed()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Agents:LocalAuth:OwnerId"] = Guid.NewGuid().ToString(),
            ["Agents:LocalAuth:Token"] = new string('x', 64)
        };

        var identity = LocalIdentity.From(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), allowLocalAuth: false);

        Assert.Equal(Guid.Empty, identity.OwnerId);
        Assert.Empty(identity.TokenHash);
    }
}
