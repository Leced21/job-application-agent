using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace JobApplicationAgent.Gateway.Identity;

public sealed class GatewayIdentityDbContextFactory : IDesignTimeDbContextFactory<GatewayIdentityDbContext>
{
    public GatewayIdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__IdentityDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var database = Environment.GetEnvironmentVariable("IDENTITY_DB_NAME");
            var username = Environment.GetEnvironmentVariable("IDENTITY_DB_USER");
            var password = Environment.GetEnvironmentVariable("IDENTITY_DB_PASSWORD");
            if (string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username) || password is null)
                throw new InvalidOperationException(
                    "Set ConnectionStrings__IdentityDatabase or IDENTITY_DB_NAME, IDENTITY_DB_USER and IDENTITY_DB_PASSWORD before running Gateway Identity EF commands.");

            connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1",
                Port = 5439,
                Database = database,
                Username = username,
                Password = password
            }.ConnectionString;
        }

        var options = new DbContextOptionsBuilder<GatewayIdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new GatewayIdentityDbContext(options);
    }
}
