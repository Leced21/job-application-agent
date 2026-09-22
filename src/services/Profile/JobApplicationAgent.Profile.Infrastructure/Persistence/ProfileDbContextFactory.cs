using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence
{
    public sealed class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
    {
        public ProfileDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ProfileDbContext>();

            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=profile_db;Username=jobagent;Password=jobagent_dev");

            return new ProfileDbContext(optionsBuilder.Options);
        }
    }
}
