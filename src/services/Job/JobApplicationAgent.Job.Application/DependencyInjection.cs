using JobApplicationAgent.Job.Application.Jobs.Status;
using FluentValidation;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Application.Jobs.Delete;
using JobApplicationAgent.Job.Application.Jobs.Get;
using JobApplicationAgent.Job.Application.Jobs.Update;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Job.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly);

        services.AddScoped<CreateJobHandler>();
        services.AddScoped<GetJobsHandler>();
        services.AddScoped<GetJobByIdHandler>();
        services.AddScoped<UpdateJobHandler>();
        services.AddScoped<DeleteJobHandler>();
        services.AddScoped<ChangeJobStatusHandler>();

        return services;
    }
}