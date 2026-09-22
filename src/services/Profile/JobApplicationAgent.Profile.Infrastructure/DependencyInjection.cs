using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using JobApplicationAgent.Profile.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Profile.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ProfileDbContext>(options =>
            {
                // Read after the host has applied all configuration sources.
                var connectionString = configuration.GetConnectionString("ProfileDatabase")
                    ?? throw new InvalidOperationException("Connection string 'ProfileDatabase' was not found.");

                options.UseNpgsql(connectionString);
            });

            services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();

            return services;
        }
    }
}
