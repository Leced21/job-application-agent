using FluentValidation;
using JobApplicationAgent.Profile.Application.Profiles.Create;
using JobApplicationAgent.Profile.Application.Profiles.Get;
using Microsoft.Extensions.DependencyInjection;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Get;

namespace JobApplicationAgent.Profile.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddScoped<CreateCandidateProfileHandler>();
            services.AddScoped<GetCandidateProfileHandler>();
            services.AddScoped<AddProfessionalExperienceHandler>();
            services.AddScoped<GetProfessionalExperiencesHandler>();
            return services;
        }
    }
}
