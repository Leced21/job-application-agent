using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class LanguageConfiguration
    : IEntityTypeConfiguration<Language>
{
    public void Configure(
        EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CandidateProfileId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProficiencyLevel)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.CandidateProfileId);
    }
}