using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations
{
    public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
    {
        public void Configure(EntityTypeBuilder<CandidateProfile> builder)
        {
            builder.ToTable("candidate_profiles");

            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.Preferences)
                .WithOne()
                .HasForeignKey<CandidatePreferences>(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(x => x.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.LastName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.Email)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(x => x.PhoneNumber)
                .HasMaxLength(30);

            builder.Property(x => x.JobTitle)
                .HasMaxLength(150);

            builder.Property(x => x.Summary)
                .HasMaxLength(2000);

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            builder.HasIndex(x => x.Email)
                .IsUnique();
            builder.HasMany(x => x.ProfessionalExperiences)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(x => x.ProfessionalExperiences)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(x => x.Educations)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Skills)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Languages)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Links)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(x => x.Certifications)
                .WithOne()
                .HasForeignKey(x => x.CandidateProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
