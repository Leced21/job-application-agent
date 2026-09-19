using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence
{
    public sealed class ProfileDbContext (DbContextOptions<ProfileDbContext> options) : DbContext(options)
    {
        public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfileDbContext).Assembly);
        }
    }
}
