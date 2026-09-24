using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobApplicationAgent.Job.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration
    : IEntityTypeConfiguration<JobEntity>
{
    public void Configure(
        EntityTypeBuilder<JobEntity> builder)
    {
        builder.ToTable("job_offers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Location)
            .HasColumnName("location")
            .HasMaxLength(200);

        builder.Property(x => x.WorkMode)
            .HasColumnName("work_mode")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ContractType)
            .HasColumnName("contract_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SalaryMin)
            .HasColumnName("salary_min")
            .HasPrecision(12, 2);

        builder.Property(x => x.SalaryMax)
            .HasColumnName("salary_max")
            .HasPrecision(12, 2);

        builder.Property(x => x.SalaryCurrency)
            .HasColumnName("salary_currency")
            .HasMaxLength(3);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(10_000);

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(2_000);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PublishedAtUtc)
            .HasColumnName("published_at_utc");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.CompanyName);

        builder.HasIndex(x => x.PublishedAtUtc);
    }
}