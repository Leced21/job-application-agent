using JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Matching.Infrastructure.Persistence;

public sealed class MatchingDbContext(
DbContextOptions<MatchingDbContext> options) : DbContext(options)
{
    public DbSet<MatchingResultRecord> MatchingResults =>
    Set<MatchingResultRecord>();
    public DbSet<MatchingCriterionRecord> MatchingCriteria =>
    Set<MatchingCriterionRecord>();
    public DbSet<JobPublishedEventInboxRecord> JobPublishedEvents => Set<JobPublishedEventInboxRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MatchingResultRecord>(entity =>
        {
            entity.ToTable("matching_results");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ScoringVersion).HasMaxLength(128).IsRequired().HasDefaultValue("legacy-v1");
            entity.Property(x => x.Status)
    .HasMaxLength(50)
    .IsRequired();
            entity.Property(x => x.OverallScore)
    .HasPrecision(5, 2);
            entity.Property(x => x.EvaluationCoverage)
    .HasPrecision(5, 2);
            entity.HasIndex(x => x.CandidateProfileId);
            entity.HasIndex(x => x.JobId);
            entity.HasMany(x => x.Criteria)
    .WithOne(x => x.MatchingResult)
    .HasForeignKey(x => x.MatchingResultId)
    .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MatchingCriterionRecord>(entity =>
        {
            entity.ToTable("matching_criteria");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name)
    .HasMaxLength(100)
    .IsRequired();
            entity.Property(x => x.Status)
    .HasMaxLength(50)
    .IsRequired();
            entity.Property(x => x.Score)
    .HasPrecision(5, 2);
            entity.Property(x => x.Weight)
    .HasPrecision(5, 4);
            entity.Property(x => x.Explanation)
    .HasColumnType("text");
        });
        modelBuilder.Entity<JobPublishedEventInboxRecord>(entity =>
        {
            entity.ToTable("matching_event_inbox");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.PayloadSha256).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.JobId, x.ProcessedAtUtc });
        });
    }
}
