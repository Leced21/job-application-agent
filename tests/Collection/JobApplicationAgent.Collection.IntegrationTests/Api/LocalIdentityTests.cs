using JobApplicationAgent.Collection.Api;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Collection.IntegrationTests.Api;

public sealed class LocalIdentityTests
{
    [Fact]
    public void From_ShouldDisableStaticIdentity_WhenLocalAuthenticationIsNotAllowed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Collection:LocalAuth:Token"] = new string('x', 64)
        }).Build();

        var identity = LocalIdentity.From(configuration, allowLocalAuth: false);

        Assert.False(identity.Enabled);
        Assert.Empty(identity.TokenHash);
    }
}
