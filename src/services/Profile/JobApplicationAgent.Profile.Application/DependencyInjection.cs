using FluentValidation;
using JobApplicationAgent.Profile.Application.Profiles.Create;
using JobApplicationAgent.Profile.Application.Profiles.Get;
using Microsoft.Extensions.DependencyInjection;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Add;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Get;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Update;
using JobApplicationAgent.Profile.Application.Profiles.Experiences.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Add;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Get;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Update;
using JobApplicationAgent.Profile.Application.Profiles.Educations.Delete;

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
            services.AddScoped<UpdateProfessionalExperienceHandler>();
            services.AddScoped<DeleteProfessionalExperienceHandler>();
            services.AddScoped<AddEducationHandler>();
            services.AddScoped<GetEducationsHandler>();
            services.AddScoped<UpdateEducationHandler>();
            services.AddScoped<DeleteEducationHandler>();
            return services;
        }
    }
}
