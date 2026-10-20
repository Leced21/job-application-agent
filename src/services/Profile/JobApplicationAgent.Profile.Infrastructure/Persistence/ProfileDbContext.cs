using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence
{
    public sealed class ProfileDbContext (DbContextOptions<ProfileDbContext> options) : DbContext(options)
    {
        public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
        public DbSet<DocumentFactImport> DocumentFactImports => Set<DocumentFactImport>();
        public DbSet<ProfessionalExperience> ProfessionalExperiences =>Set<ProfessionalExperience>();
        public DbSet<Education> Educations => Set<Education>();
        public DbSet<Skill> Skills => Set<Skill>();
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<Link> Links => Set<Link>();
        public DbSet<Certification> Certifications => Set<Certification>();
        public DbSet<CandidatePreferences> Preferences => Set<CandidatePreferences>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfileDbContext).Assembly);
            var receipt = modelBuilder.Entity<DocumentFactImport>();
            receipt.ToTable("document_fact_imports");
            receipt.HasKey(x => x.FactId);
            receipt.Property(x => x.Kind).HasMaxLength(30).HasDefaultValue("Skill").IsRequired();
            receipt.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
            receipt.Property(x => x.Action).HasMaxLength(30).IsRequired();
            receipt.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
            receipt.Property(x => x.RequestJson).HasColumnType("jsonb").IsRequired();
        }
    }
}
