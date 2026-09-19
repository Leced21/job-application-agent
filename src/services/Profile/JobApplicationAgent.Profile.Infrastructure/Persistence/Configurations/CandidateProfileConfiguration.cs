using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations
{
    public sealed class CandidateProfileConfiguration:IEntityTypeConfiguration<CandidateProfile>
    {
        public void Configure(EntityTypeBuilder<CandidateProfile> builder)
        {
            builder.ToTable("candidate_profiles");

            builder.HasKey(x => x.Id);

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
        }
    }
}
