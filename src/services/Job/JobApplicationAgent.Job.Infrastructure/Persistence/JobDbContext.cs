using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace JobApplicationAgent.Job.Infrastructure.Persistence;
public sealed class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobDbContext).Assembly);
    }
}
