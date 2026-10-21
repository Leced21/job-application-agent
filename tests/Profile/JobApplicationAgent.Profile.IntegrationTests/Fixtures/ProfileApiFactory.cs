using JobApplicationAgent.Profile.Domain.Entities;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace JobApplicationAgent.Profile.IntegrationTests.Fixtures;

public sealed class ProfileApiFactory :
    WebApplicationFactory<Program>,
    IAsyncLifetime
{
    public const string ServiceToken = "profile-integration-test-service-token-with-strong-length";
    public static readonly Guid OwnerId = Guid.Parse("48c6a244-09d4-405f-b46e-74655149a352");
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17")
            .WithDatabase("profile_test_db")
            .WithUsername("jobagent")
            .WithPassword("jobagent_test")
            .Build();

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:ProfileDatabase"] =
                            _postgres.GetConnectionString(),
                        ["Profile:LocalAuth:Token"] = ServiceToken,
                        ["Profile:LocalAuth:OwnerId"] = OwnerId.ToString()
                    });
            });
    }

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ServiceToken);
        return client;
    }

    public HttpClient CreateUnauthenticatedClient() => base.CreateClient();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ProfileDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ProfileDbContext>();

        await dbContext.CandidateProfiles
            .ExecuteDeleteAsync();
    }

    public async Task<Education?> GetEducationAsync(
        Guid educationId)
    {
        using var scope = Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ProfileDbContext>();

        return await dbContext.Educations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == educationId);
    }

    public async Task<Skill?> GetSkillAsync(
        Guid skillId)
    {
        using var scope = Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ProfileDbContext>();

        return await dbContext.Skills
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == skillId);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        await _postgres.DisposeAsync();
    }
    public async Task<Language?> GetLanguageAsync(Guid languageId)
    {
        using var scope = Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ProfileDbContext>();

        return await dbContext.Languages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == languageId);
    }
}
