using JobApplicationAgent.Profile.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Configurations;

public sealed class CandidatePreferencesConfiguration : IEntityTypeConfiguration<CandidatePreferences>
{
    public void Configure(EntityTypeBuilder<CandidatePreferences> builder)
    {
        builder.ToTable("candidate_preferences");
        builder.HasKey(x => x.CandidateProfileId);
        builder.Property(x => x.CandidateProfileId).ValueGeneratedNever();
        builder.Property(x => x.DesiredJobTitles).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.PreferredLocations).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.ContractTypes).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.WorkModes).HasColumnType("text[]").IsRequired();
        builder.Property(x => x.MinimumAnnualGrossSalary).HasPrecision(12, 2);
        builder.Property(x => x.SalaryCurrency).HasMaxLength(3);
        builder.Property(x => x.AvailableFrom);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
