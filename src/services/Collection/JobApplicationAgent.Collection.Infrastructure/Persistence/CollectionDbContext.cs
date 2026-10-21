using JobApplicationAgent.Collection.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Collection.Infrastructure.Persistence;

public sealed class CollectionDbContext(DbContextOptions<CollectionDbContext> options) : DbContext(options)
{
    public DbSet<CollectedOffer> Offers => Set<CollectedOffer>();
    public DbSet<OfferRevision> Revisions => Set<OfferRevision>();
    public DbSet<CollectionOutboxMessage> OutboxMessages => Set<CollectionOutboxMessage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var offer = modelBuilder.Entity<CollectedOffer>();
        offer.ToTable("collected_offers");
        offer.HasKey(x => x.Id);
        offer.Property(x => x.Title).HasMaxLength(200).IsRequired();
        offer.Property(x => x.OriginalTitle).HasMaxLength(200).IsRequired();
        offer.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
        offer.Property(x => x.Location).HasMaxLength(200);
        offer.Property(x => x.WorkMode).HasMaxLength(50).IsRequired();
        offer.Property(x => x.ContractType).HasMaxLength(50).IsRequired();
        offer.Property(x => x.SalaryMin).HasPrecision(12, 2);
        offer.Property(x => x.SalaryMax).HasPrecision(12, 2);
        offer.Property(x => x.SalaryCurrency).HasMaxLength(3);
        offer.Property(x => x.Description).HasMaxLength(10_000);
        offer.Property(x => x.Skills).HasColumnType("text[]").IsRequired();
        offer.Property(x => x.SourceName).HasMaxLength(100).IsRequired();
        offer.Property(x => x.SourceUrl).HasMaxLength(2000);
        offer.Property(x => x.DeduplicationKey).HasMaxLength(64).IsRequired();
        offer.Property(x => x.ContentFingerprint).HasMaxLength(64).IsRequired();
        offer.Property(x => x.NormalizationVersion).HasMaxLength(50).IsRequired();
        offer.HasIndex(x => x.DeduplicationKey).IsUnique();
        offer.HasIndex(x => new { x.FirstCollectedAtUtc, x.Id });
        offer.HasIndex(x => x.ExpiresAtUtc);
        var revision = modelBuilder.Entity<OfferRevision>();
        revision.ToTable("offer_revisions");
        revision.HasKey(x => x.Id);
        revision.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        revision.Property(x => x.OriginalTitle).HasMaxLength(200).IsRequired();
        revision.Property(x => x.ContentJson).HasColumnType("jsonb").IsRequired();
        revision.Property(x => x.NormalizationVersion).HasMaxLength(50).IsRequired();
        revision.Property(x => x.Decision).HasMaxLength(20).IsRequired();
        revision.Property(x => x.ReviewedByOwnerId);
        revision.HasIndex(x => new { x.OfferId, x.Fingerprint }).IsUnique();
        revision.HasIndex(x => new { x.OfferId, x.CollectedAtUtc, x.Id });
        revision.HasOne<CollectedOffer>().WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        var message = modelBuilder.Entity<CollectionOutboxMessage>();
        message.ToTable("collection_outbox");
        message.HasKey(x => x.Id);
        message.Property(x => x.EventType).HasMaxLength(150).IsRequired();
        message.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        message.Property(x => x.CorrelationId).HasMaxLength(36);
        message.Property(x => x.LastFailureCode).HasMaxLength(80);
        message.HasIndex(x => x.RevisionId).IsUnique();
        message.HasIndex(x => new { x.OccurredAtUtc, x.Id }).HasFilter("\"DispatchedAtUtc\" IS NULL");
        message.HasIndex(x => new { x.NextAttemptAtUtc, x.OccurredAtUtc, x.Id })
            .HasFilter("\"DispatchedAtUtc\" IS NULL");
    }
}
