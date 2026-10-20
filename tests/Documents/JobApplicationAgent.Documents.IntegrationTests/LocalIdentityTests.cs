using JobApplicationAgent.Documents.Api;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class LocalIdentityTests
{
    [Theory]
    [InlineData("missing-token")]
    [InlineData("weak-token")]
    [InlineData("missing-owner")]
    public void MissingIdentityConfiguration_ShouldFailClosed(string invalid)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Documents:LocalAuth:OwnerId"] = invalid == "missing-owner" ? null : Guid.NewGuid().ToString(),
            ["Documents:LocalAuth:Token"] = invalid == "missing-token" ? null : invalid == "weak-token" ? "weak" : Guid.NewGuid().ToString("N")
        };
        Assert.Throws<InvalidOperationException>(() => LocalIdentity.From(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), allowLocalAuth: true));
    }

    [Fact]
    public void LocalAuthenticationOutsideLocalEnvironments_ShouldDisableStaticIdentity()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Documents:LocalAuth:OwnerId"] = Guid.NewGuid().ToString(),
            ["Documents:LocalAuth:Token"] = new string('x', 64)
        };

        var identity = LocalIdentity.From(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), allowLocalAuth: false);

        Assert.Equal(Guid.Empty, identity.OwnerId);
        Assert.Empty(identity.TokenHash);
    }
}
