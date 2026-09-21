using JobApplicationAgent.Profile.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using JobApplicationAgent.Profile.Domain.Entities;

namespace JobApplicationAgent.Profile.IntegrationTests.Fixtures
{
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

        public async Task InitializeAsync()
        {
            // 1. Démarrage du PostgreSQL de test
            await _postgres.StartAsync();

            // 2. Injection AVANT le démarrage de Profile.Api
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__ProfileDatabase",
                _postgres.GetConnectionString());

            // 3. Le premier accès à Services démarre réellement l'API
            using var scope = Services.CreateScope();

            var dbContext =
                scope.ServiceProvider.GetRequiredService<ProfileDbContext>();

            // 4. Application des migrations sur la DB temporaire
            await dbContext.Database.MigrateAsync();
        }

        public new async Task DisposeAsync()
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__ProfileDatabase",
                null);

            await _postgres.DisposeAsync();

            await base.DisposeAsync();
        }
        public async Task ResetDatabaseAsync()
        {
            using var scope = Services.CreateScope();

            var dbContext =
                scope.ServiceProvider.GetRequiredService<ProfileDbContext>();

            await dbContext.CandidateProfiles.ExecuteDeleteAsync();
        }
        public async Task<Education?> GetEducationAsync(Guid educationId)
        {
            using var scope = Services.CreateScope();

            var dbContext =
                scope.ServiceProvider.GetRequiredService<ProfileDbContext>();

            return await dbContext.Educations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == educationId);
        }
       
    }
}

