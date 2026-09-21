using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class ProfessionalExperienceConfiguration
    : IEntityTypeConfiguration<ProfessionalExperience>
{
    public void Configure(
        EntityTypeBuilder<ProfessionalExperience> builder)
    {
        builder.ToTable("professional_experiences");

        builder.HasKey(x => x.Id);

        // The domain assigns the ID before this entity joins a tracked profile.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.JobTitle)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Location)
            .HasMaxLength(200);

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.EndDate);

        builder.Property(x => x.IsCurrent)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.CandidateProfileId);
    }
}
