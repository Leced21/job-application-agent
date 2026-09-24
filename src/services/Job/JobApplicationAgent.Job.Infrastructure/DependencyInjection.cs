using JobApplicationAgent.Job.Application.Abstractions;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Job.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<JobDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("JobDatabase")
                ?? throw new InvalidOperationException("Connection string 'JobDatabase' was not found.");
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IJobRepository, JobRepository>();

        return services;
    }
}