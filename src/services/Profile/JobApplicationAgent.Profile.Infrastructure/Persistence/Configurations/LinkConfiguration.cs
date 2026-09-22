using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class LinkConfiguration
    : IEntityTypeConfiguration<Link>
{
    public void Configure(
        EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("links");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.CandidateProfileId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.CandidateProfileId);
    }
}