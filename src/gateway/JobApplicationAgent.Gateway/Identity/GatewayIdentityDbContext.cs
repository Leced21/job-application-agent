using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Gateway.Identity;

public sealed class GatewayIdentityDbContext(
    DbContextOptions<GatewayIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.OwnerId)
            .IsUnique()
            .HasFilter("\"OwnerId\" IS NOT NULL");
        builder.UseOpenIddict();
    }
}
