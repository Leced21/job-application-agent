extern alias DocumentsSource;
using Microsoft.Extensions.Configuration;
using Channel = DocumentsSource::JobApplicationAgent.Documents.Api.ApplicationBindingChannel;

namespace JobApplicationAgent.Applications.IntegrationTests;

public class ApplicationChannelConfigurationTests
{
    [Fact]
    public void AbsentChannelPreservesExistingDocumentsConfiguration()
    {
        var channel = Channel.From(new ConfigurationBuilder().Build());
        Assert.False(channel.Scope.Enabled); Assert.Empty(channel.TokenHash);
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("profile")]
    [InlineData("token")]
    [InlineData("reuse")]
    public void InvalidOrPartiallyConfiguredChannelFailsClosed(string field)
    {
        var owner = Guid.NewGuid(); var token = new string('c', 64);
        var config = new Dictionary<string, string?> {
            ["Documents:LocalAuth:OwnerId"] = owner.ToString(), ["Documents:LocalAuth:Token"] = new string('a', 64),
            ["Documents:Applications:OwnerId"] = owner.ToString(), ["Documents:Applications:ProfileId"] = Guid.NewGuid().ToString(),
            ["Documents:Applications:Token"] = token };
        if (field == "reuse") config["Documents:Applications:Token"] = new string('a', 64);
        else config["Documents:Applications:" + (field == "owner" ? "OwnerId" : field == "profile" ? "ProfileId" : "Token")] = null;
        Assert.Throws<InvalidOperationException>(() => Channel.From(new ConfigurationBuilder().AddInMemoryCollection(config).Build()));
    }
    [Fact]
    public void OwnerBindingUsesUuidEqualityRatherThanTextCasing()
    {
        var owner = Guid.NewGuid();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Documents:LocalAuth:OwnerId"] = owner.ToString().ToUpperInvariant(), ["Documents:LocalAuth:Token"] = new string('a', 64),
            ["Documents:Applications:OwnerId"] = owner.ToString(), ["Documents:Applications:ProfileId"] = Guid.NewGuid().ToString(),
            ["Documents:Applications:Token"] = new string('c', 64) }).Build();
        Assert.True(Channel.From(config).Scope.Enabled);
    }
}
