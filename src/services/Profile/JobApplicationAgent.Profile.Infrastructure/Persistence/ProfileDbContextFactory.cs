using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence
{
    public sealed class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
    {
        public ProfileDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ProfileDbContext>();

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ProfileDatabase")
                ?? "Host=localhost;Port=5432;Database=profile_db;Username=jobagent;Password=jobagent_dev";
            optionsBuilder.UseNpgsql(connectionString);

            return new ProfileDbContext(optionsBuilder.Options);
        }
    }
}
