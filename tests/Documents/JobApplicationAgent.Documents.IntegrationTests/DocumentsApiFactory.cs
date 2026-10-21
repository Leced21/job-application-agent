using JobApplicationAgent.Documents.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class DocumentsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").WithDatabase("documents_tests")
        .WithUsername("test_" + Guid.NewGuid().ToString("N")[..8]).WithPassword(Guid.NewGuid().ToString("N")).Build();
    public Guid OwnerId { get; } = Guid.NewGuid();
    public string Token { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DocumentsDatabase"] = postgres.GetConnectionString(),
            ["Documents:LocalAuth:OwnerId"] = OwnerId.ToString(), ["Documents:LocalAuth:Token"] = Token,
            ["Documents:Extraction:WorkerPath"] = Path.Combine(AppContext.BaseDirectory, "ExtractionWorker", "JobApplicationAgent.Documents.ExtractionWorker.dll")
        }));
    }
    public HttpClient AuthorizedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token);
        return client;
    }
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.MigrateAsync();
    }
    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await db.ApplicationBindings.ExecuteDeleteAsync();
        await db.Exports.ExecuteDeleteAsync();
        await db.Compositions.ExecuteDeleteAsync();
        await db.PreparedDrafts.ExecuteDeleteAsync();
        await db.ResumeSeries.ExecuteDeleteAsync();
        await db.Resumes.ExecuteDeleteAsync();
    }
    public new async Task DisposeAsync() { await base.DisposeAsync(); await postgres.DisposeAsync(); }
}
