using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class EducationConfiguration
    : IEntityTypeConfiguration<Education>
{
    public void Configure(
        EntityTypeBuilder<Education> builder)
    {
        builder.ToTable("educations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CandidateProfileId)
            .IsRequired();

        builder.Property(x => x.InstitutionName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Degree)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.FieldOfStudy)
            .HasMaxLength(200);

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