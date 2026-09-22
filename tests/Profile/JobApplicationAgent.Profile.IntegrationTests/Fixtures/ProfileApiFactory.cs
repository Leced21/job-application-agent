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
                            _postgres.GetConnectionString()
                    });
            });
    }

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