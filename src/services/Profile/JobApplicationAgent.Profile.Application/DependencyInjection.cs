using JobApplicationAgent.Profile.Application.Profiles.Preferences.Get;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Update;
using JobApplicationAgent.Profile.Application.Profiles.Preferences.Delete;
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
using JobApplicationAgent.Profile.Application.Profiles.Skills.Add;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Get;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Update;
using JobApplicationAgent.Profile.Application.Profiles.Skills.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Add;
using JobApplicationAgent.Profile.Application.Profiles.Links.Add;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Add;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Get;
using JobApplicationAgent.Profile.Application.Profiles.Links.Get;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Get;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Update;
using JobApplicationAgent.Profile.Application.Profiles.Links.Update;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Update;
using JobApplicationAgent.Profile.Application.Profiles.Languages.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Links.Delete;
using JobApplicationAgent.Profile.Application.Profiles.Certifications.Delete;


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
            services.AddScoped<AddSkillHandler>();
            services.AddScoped<GetSkillsHandler>();
            services.AddScoped<UpdateSkillHandler>();
            services.AddScoped<DeleteSkillHandler>();
            services.AddScoped<AddLanguageHandler>();
            services.AddScoped<AddLinkHandler>();
            services.AddScoped<AddCertificationHandler>();
            services.AddScoped<GetLanguagesHandler>();
            services.AddScoped<GetLinksHandler>();
            services.AddScoped<GetCertificationsHandler>();
            services.AddScoped<UpdateLanguageHandler>();
            services.AddScoped<UpdateLinkHandler>();
            services.AddScoped<UpdateCertificationHandler>();
            services.AddScoped<DeleteLanguageHandler>();
            services.AddScoped<DeleteLinkHandler>();
            services.AddScoped<DeleteCertificationHandler>();
            services.AddScoped<GetPreferencesHandler>();
            services.AddScoped<UpdatePreferencesHandler>();
            services.AddScoped<DeletePreferencesHandler>();
            return services;
        }
    }
}
