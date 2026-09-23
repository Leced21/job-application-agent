using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class CertificationConfiguration
    : IEntityTypeConfiguration<Certification>
{
    public void Configure(
        EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("certifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CandidateProfileId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.IssuingOrganization)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.IssueDate).IsRequired();
        builder.Property(x => x.ExpirationDate);
        builder.Property(x => x.CredentialId).HasMaxLength(200);
        builder.Property(x => x.CredentialUrl).HasMaxLength(2000);

        builder.HasIndex(x => x.CandidateProfileId);
    }
}