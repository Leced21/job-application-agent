using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Collection.Infrastructure.Persistence;

public sealed class CollectionDbContextFactory : IDesignTimeDbContextFactory<CollectionDbContext>
{
    public CollectionDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__CollectionDatabase")
            ?? throw new InvalidOperationException("Set ConnectionStrings__CollectionDatabase for EF commands.");
        return new CollectionDbContext(new DbContextOptionsBuilder<CollectionDbContext>().UseNpgsql(connection).Options);
    }
}
