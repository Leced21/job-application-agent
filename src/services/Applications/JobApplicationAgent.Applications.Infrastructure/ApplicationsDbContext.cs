using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Applications.Infrastructure;

public sealed class CandidacyRow
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ProfileId { get; set; }
    public Guid JobId { get; set; }
    public string JobTitle { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string HistoryJson { get; set; } = "";
    public int Status { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? NextFollowUpId { get; set; }
    public DateTime? NextFollowUpDueAtUtc { get; set; }
    public int? NextFollowUpChannel { get; set; }
    public string? NextFollowUpNote { get; set; }
}
public sealed class CandidacyRequestRow
{
    public Guid OwnerId { get; set; }
    public Guid RequestId { get; set; }
    public Guid CandidacyId { get; set; }
}
public sealed class ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options) : DbContext(options)
{
    public DbSet<CandidacyRow> Candidacies => Set<CandidacyRow>();
    public DbSet<CandidacyRequestRow> Requests => Set<CandidacyRequestRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var c = model.Entity<CandidacyRow>();
        c.ToTable("Candidacies", t =>
        {
            t.HasCheckConstraint("CK_Candidacy_State", "\"Status\" IN (1,2,3,4,5,6,7,8,9) AND \"Revision\" BETWEEN 1 AND 1000");
            t.HasCheckConstraint("CK_Candidacy_History", "octet_length(\"HistoryJson\") <= 16777216");
            t.HasCheckConstraint("CK_Candidacy_Dates", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            t.HasCheckConstraint("CK_Candidacy_FollowUp", """
                ("NextFollowUpId" IS NULL AND "NextFollowUpDueAtUtc" IS NULL AND "NextFollowUpChannel" IS NULL AND "NextFollowUpNote" IS NULL)
                OR ("NextFollowUpId" IS NOT NULL AND "NextFollowUpId" <> '00000000-0000-0000-0000-000000000000'
                  AND "NextFollowUpDueAtUtc" IS NOT NULL AND "NextFollowUpChannel" IS NOT NULL AND "NextFollowUpChannel" BETWEEN 1 AND 6
                  AND "NextFollowUpNote" IS NOT NULL AND char_length("NextFollowUpNote") BETWEEN 1 AND 1000 AND "Status" IN (5,6,7))
                """);
            t.HasCheckConstraint("CK_Candidacy_Ids", "\"OwnerId\" <> '00000000-0000-0000-0000-000000000000' AND \"ProfileId\" <> '00000000-0000-0000-0000-000000000000' AND \"JobId\" <> '00000000-0000-0000-0000-000000000000'");
        });
        c.HasKey(x => x.Id); c.HasAlternateKey(x => new { x.OwnerId, x.Id });
        c.HasIndex(x => new { x.OwnerId, x.ProfileId, x.JobId }).IsUnique();
        c.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc, x.Id });
        c.Property(x => x.JobTitle).HasMaxLength(200); c.Property(x => x.CompanyName).HasMaxLength(200);
        c.Property(x => x.HistoryJson).HasColumnType("text");
        c.Property(x => x.NextFollowUpNote).HasMaxLength(1000);
        c.HasIndex(x => new { x.OwnerId, x.NextFollowUpDueAtUtc, x.Id }).HasFilter("\"NextFollowUpId\" IS NOT NULL");
        var r = model.Entity<CandidacyRequestRow>();
        r.ToTable("CandidacyRequests"); r.HasKey(x => new { x.OwnerId, x.RequestId });
        r.HasOne<CandidacyRow>().WithMany().HasForeignKey(x => new { x.OwnerId, x.CandidacyId })
            .HasPrincipalKey(x => new { x.OwnerId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>
{
    public ApplicationsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ApplicationsDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__ApplicationsDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__ApplicationsDatabase.")).Options);
}
