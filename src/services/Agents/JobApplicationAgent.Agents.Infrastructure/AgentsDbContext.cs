using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed class AgentRunRow
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid RequestId { get; set; }
    public int State { get; set; }
    public string Snapshot { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public long? ReservedTokens { get; set; }
    public decimal? ReservedUsd { get; set; }
    public long ChargedTokens { get; set; }
    public decimal ChargedUsd { get; set; }
}
public sealed class AgentAuditRow
{
    public Guid RunId { get; set; }
    public long Revision { get; set; }
    public string Metadata { get; set; } = "";
}
public sealed class AgentsDbContext(DbContextOptions<AgentsDbContext> options) : DbContext(options)
{
    public DbSet<AgentRunRow> Runs => Set<AgentRunRow>();
    public DbSet<AgentAuditRow> Audits => Set<AgentAuditRow>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var runs = model.Entity<AgentRunRow>();
        runs.ToTable("agent_runs", t => {
            t.HasCheckConstraint("ck_agent_charge", "\"ChargedTokens\" >= 0 AND \"ChargedUsd\" >= 0");
            t.HasCheckConstraint("ck_agent_reservation", "(\"ReservedTokens\" IS NULL AND \"ReservedUsd\" IS NULL) OR (\"ReservedTokens\" IS NOT NULL AND \"ReservedUsd\" IS NOT NULL AND \"ReservedTokens\" >= 0 AND \"ReservedUsd\" >= 0 AND \"State\" = 1)");
        });
        runs.HasKey(x => x.Id);
        runs.HasIndex(x => new { x.OwnerId, x.RequestId }).IsUnique();
        runs.HasIndex(x => new { x.OwnerId, x.ExpiresAtUtc });
        runs.Property(x => x.Snapshot).HasColumnType("text");
        runs.Property(x => x.ReservedUsd).HasColumnType("numeric");
        runs.Property(x => x.ChargedUsd).HasColumnType("numeric");
        var audits = model.Entity<AgentAuditRow>();
        audits.ToTable("agent_audits");
        audits.HasKey(x => new { x.RunId, x.Revision });
        audits.HasOne<AgentRunRow>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class AgentsDesignFactory : IDesignTimeDbContextFactory<AgentsDbContext>
{
    public AgentsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AgentsDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__AgentsDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__AgentsDatabase outside Git.")).Options);
}
