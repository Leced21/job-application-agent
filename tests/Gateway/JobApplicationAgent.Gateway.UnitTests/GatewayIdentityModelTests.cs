using JobApplicationAgent.Gateway.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Gateway.UnitTests;

public sealed class GatewayIdentityModelTests
{
    [Fact]
    public void IdentityModel_ContainsIdentityAndOpenIddictTables()
    {
        var options = new DbContextOptionsBuilder<GatewayIdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_model;Username=test;Password=test")
            .Options;
        using var context = new GatewayIdentityDbContext(options);

        var tables = context.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("AspNetUsers", tables);
        Assert.Contains("AspNetUserRoles", tables);
        Assert.Contains("OpenIddictApplications", tables);
        Assert.Contains("OpenIddictAuthorizations", tables);
        Assert.Contains("OpenIddictScopes", tables);
        Assert.Contains("OpenIddictTokens", tables);
        Assert.Equal(typeof(Guid), typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Id))!.PropertyType);
        Assert.Equal(typeof(Guid?), typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.OwnerId))!.PropertyType);
        var ownerIndex = context.Model.FindEntityType(typeof(ApplicationUser))!.GetIndexes()
            .Single(index => index.Properties.Single().Name == nameof(ApplicationUser.OwnerId));
        Assert.True(ownerIndex.IsUnique);
    }
}
