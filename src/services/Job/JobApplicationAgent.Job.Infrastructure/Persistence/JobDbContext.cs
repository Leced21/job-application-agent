using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace JobApplicationAgent.Job.Infrastructure.Persistence;
public sealed class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<CollectionPublication> CollectionPublications => Set<CollectionPublication>();
    public DbSet<JobOutboxMessage> OutboxMessages => Set<JobOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobDbContext).Assembly);
        var receipt = modelBuilder.Entity<CollectionPublication>();
        receipt.ToTable("collection_publications");
        receipt.HasKey(x => x.OfferId);
        receipt.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        receipt.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        receipt.Property(x => x.RequestJson).HasColumnType("jsonb").IsRequired();
        var outbox = modelBuilder.Entity<JobOutboxMessage>();
        outbox.ToTable("job_outbox");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.EventType).HasMaxLength(150).IsRequired();
        outbox.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        outbox.Property(x => x.CorrelationId).HasMaxLength(36);
        outbox.Property(x => x.LastFailureCode).HasMaxLength(80);
        outbox.HasIndex(x => new { x.OccurredAtUtc, x.Id }).HasFilter("\"DispatchedAtUtc\" IS NULL");
        outbox.HasIndex(x => new { x.NextAttemptAtUtc, x.OccurredAtUtc, x.Id })
            .HasFilter("\"DispatchedAtUtc\" IS NULL");
    }
}
