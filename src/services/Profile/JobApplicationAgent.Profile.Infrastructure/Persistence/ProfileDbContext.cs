using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence
{
    public sealed class ProfileDbContext (DbContextOptions<ProfileDbContext> options) : DbContext(options)
    {
        public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
        public DbSet<ProfessionalExperience> ProfessionalExperiences =>Set<ProfessionalExperience>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfileDbContext).Assembly);
        }
    }
}
