using JobApplicationAgent.Gateway.Identity;

namespace JobApplicationAgent.Gateway.UnitTests;

public sealed class AccountOwnerIdResolverTests
{
    private static readonly Guid InitialOwnerId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Fact]
    public void CanRegister_FirstAccountRequiresConfiguredOwner()
    {
        Assert.True(AccountOwnerIdResolver.CanRegister(true, false, InitialOwnerId.ToString()));
        Assert.False(AccountOwnerIdResolver.CanRegister(true, false, null));
        Assert.False(AccountOwnerIdResolver.CanRegister(true, false, Guid.Empty.ToString()));
        Assert.False(AccountOwnerIdResolver.CanRegister(false, false, InitialOwnerId.ToString()));
    }

    [Fact]
    public void Resolve_FirstAccountUsesConfiguredOwner()
    {
        Assert.Equal(InitialOwnerId, AccountOwnerIdResolver.Resolve(false, InitialOwnerId.ToString()));
    }

    [Fact]
    public void Resolve_SubsequentAccountsReceiveDistinctOwners()
    {
        var first = AccountOwnerIdResolver.Resolve(true, InitialOwnerId.ToString());
        var second = AccountOwnerIdResolver.Resolve(true, InitialOwnerId.ToString());

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(Guid.Empty, second);
        Assert.NotEqual(InitialOwnerId, first);
        Assert.NotEqual(InitialOwnerId, second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Resolve_FirstAccountWithoutConfiguredOwnerFailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => AccountOwnerIdResolver.Resolve(false, null));
    }
}
