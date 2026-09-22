using System;
using System.Collections.Generic;
using System.Text;

namespace JobApplicationAgent.Profile.Domain.Entities
{
    public sealed class CandidateProfile
    {
        private CandidateProfile()
        {

        }

        private readonly List<ProfessionalExperience> _professionalExperiences = [];
        private readonly List<Education> _educations = [];
        private readonly List<Skill> _skills = [];
        private readonly List<Language> _languages = [];
        private readonly List<Link> _links = [];
        private readonly List<Certification> _certifications = [];

        public CandidateProfile(
            string firstName,
            string lastName,
            string email,
            string? phoneNumber = null,
            string? jobTitle = null,
            string? summary = null)
        {
            Id = Guid.NewGuid();
            FirstName = firstName;
            LastName = lastName;
            Email = email;
            PhoneNumber = phoneNumber;
            JobTitle = jobTitle;
            Summary = summary;
            CreatedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = DateTime.UtcNow;
        }
        public CandidatePreferences? Preferences { get; private set; }

        public Guid Id { get; private set; }
        public string FirstName { get; private set; } = string.Empty;
        public string LastName { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string? PhoneNumber { get; private set; }
        public string? JobTitle { get; private set; }
        public string? Summary { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }
        public IReadOnlyCollection<ProfessionalExperience> ProfessionalExperiences => _professionalExperiences;
        public IReadOnlyCollection<Education> Educations => _educations;
        public IReadOnlyCollection<Skill> Skills => _skills;
        public IReadOnlyCollection<Language> Languages => _languages;
        public IReadOnlyCollection<Link> Links => _links;
        public IReadOnlyCollection<Certification> Certifications => _certifications;
        public ProfessionalExperience AddProfessionalExperience(
            string companyName,
            string jobTitle,
            DateOnly startDate,
            DateOnly? endDate = null,
            bool isCurrent = false,
            string? location = null,
            string? description = null)
        {
            var experience = new ProfessionalExperience(
                Id,
                companyName,
                jobTitle,
                startDate,
                endDate,
                isCurrent,
                location,
                description);

            _professionalExperiences.Add(experience);

            UpdatedAtUtc = DateTime.UtcNow;

            return experience;
        }
        public ProfessionalExperience? UpdateProfessionalExperience(
            Guid experienceId,
            string companyName,
            string jobTitle,
            DateOnly startDate,
            DateOnly? endDate = null,
            bool isCurrent = false,
            string? location = null,
            string? description = null)
        {
            var experience = _professionalExperiences
                .SingleOrDefault(x => x.Id == experienceId);

            if (experience is null)
            {
                return null;
            }

            experience.Update(
                companyName,
                jobTitle,
                startDate,
                endDate,
                isCurrent,
                location,
                description);

            UpdatedAtUtc = DateTime.UtcNow;

            return experience;
        }
        public bool RemoveProfessionalExperience(Guid experienceId)
        {
            var experience = _professionalExperiences
                .SingleOrDefault(x => x.Id == experienceId);

            if (experience is null)
            {
                return false;
            }

            _professionalExperiences.Remove(experience);
            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }
        public Education AddEducation(
            string institutionName,
            string degree,
            DateOnly startDate,
            DateOnly? endDate = null,
            bool isCurrent = false,
            string? fieldOfStudy = null,
            string? location = null,
            string? description = null)
        {
            var education = new Education(
                Id,
                institutionName,
                degree,
                startDate,
                endDate,
                isCurrent,
                fieldOfStudy,
                location,
                description);

            _educations.Add(education);

            UpdatedAtUtc = DateTime.UtcNow;

            return education;
        }
        public Education? UpdateEducation(
            Guid educationId,
            string institutionName,
            string degree,
            DateOnly startDate,
            DateOnly? endDate = null,
            bool isCurrent = false,
            string? fieldOfStudy = null,
            string? location = null,
            string? description = null)
        {
            var education = _educations
                .SingleOrDefault(x => x.Id == educationId);

            if (education is null)
            {
                return null;
            }

            education.Update(
                institutionName,
                degree,
                startDate,
                endDate,
                isCurrent,
                fieldOfStudy,
                location,
                description);

            UpdatedAtUtc = DateTime.UtcNow;

            return education;
        }
        public bool RemoveEducation(Guid educationId)
        {
            var education = _educations
                .SingleOrDefault(x => x.Id == educationId);

            if (education is null)
            {
                return false;
            }

            _educations.Remove(education);

            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }
        public Skill AddSkill(
            string name,
            string? category = null,
            string? level = null,
            int? yearsOfExperience = null)
        {
            var skill = new Skill(
                Id,
                name,
                category,
                level,
                yearsOfExperience);

            _skills.Add(skill);

            UpdatedAtUtc = DateTime.UtcNow;

            return skill;
        }
        public Skill? UpdateSkill(
            Guid skillId,
            string name,
            string? category = null,
            string? level = null,
            int? yearsOfExperience = null)
        {
            var skill = _skills
                .SingleOrDefault(x => x.Id == skillId);

            if (skill is null)
            {
                return null;
            }

            skill.Update(
                name,
                category,
                level,
                yearsOfExperience);

            UpdatedAtUtc = DateTime.UtcNow;

            return skill;
        }
        public bool RemoveSkill(Guid skillId)
        {
            var skill = _skills.SingleOrDefault(x => x.Id == skillId);

            if (skill is null)
            {
                return false;
            }

            _skills.Remove(skill);
            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }
        public Language AddLanguage(
    string name,
    string proficiencyLevel)
        {
            var language = new Language(
                Id,
                name,
                proficiencyLevel);

            _languages.Add(language);
            UpdatedAtUtc = DateTime.UtcNow;

            return language;
        }

        public Language? UpdateLanguage(
            Guid languageId,
            string name,
            string proficiencyLevel)
        {
            var language =
                _languages.SingleOrDefault(x => x.Id == languageId);

            if (language is null)
                return null;

            language.Update(
                name,
                proficiencyLevel);

            UpdatedAtUtc = DateTime.UtcNow;

            return language;
        }

        public bool RemoveLanguage(Guid languageId)
        {
            var language =
                _languages.SingleOrDefault(x => x.Id == languageId);

            if (language is null)
                return false;

            _languages.Remove(language);
            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }
        public Link AddLink(
    string name,
    string url)
        {
            var link = new Link(
                Id,
                name,
                url);

            _links.Add(link);
            UpdatedAtUtc = DateTime.UtcNow;

            return link;
        }

        public Link? UpdateLink(
            Guid linkId,
            string name,
            string url)
        {
            var link =
                _links.SingleOrDefault(x => x.Id == linkId);

            if (link is null)
                return null;

            link.Update(
                name,
                url);

            UpdatedAtUtc = DateTime.UtcNow;

            return link;
        }

        public bool RemoveLink(Guid linkId)
        {
            var link =
                _links.SingleOrDefault(x => x.Id == linkId);

            if (link is null)
                return false;

            _links.Remove(link);
            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }
        public Certification AddCertification(
            string name,
            string issuingOrganization,
            DateOnly issueDate,
            DateOnly? expirationDate = null,
            string? credentialId = null,
            string? credentialUrl = null)
        {
            var certification = new Certification(
                Id,
                name,
                issuingOrganization,
                issueDate,
                expirationDate,
                credentialId,
                credentialUrl);

            _certifications.Add(certification);
            UpdatedAtUtc = DateTime.UtcNow;

            return certification;
        }

        public Certification? UpdateCertification(
            Guid certificationId,
            string name,
            string issuingOrganization,
            DateOnly issueDate,
            DateOnly? expirationDate = null,
            string? credentialId = null,
            string? credentialUrl = null)
        {
            var certification =
                _certifications.SingleOrDefault(x => x.Id == certificationId);

            if (certification is null)
                return null;

            certification.Update(
                name,
                issuingOrganization,
                issueDate,
                expirationDate,
                credentialId,
                credentialUrl);

            UpdatedAtUtc = DateTime.UtcNow;

            return certification;
        }

        public bool RemoveCertification(Guid certificationId)
        {
            var certification =
                _certifications.SingleOrDefault(x => x.Id == certificationId);

            if (certification is null)
                return false;

            _certifications.Remove(certification);
            UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }

        public CandidatePreferences SetPreferences(
            string[] desiredJobTitles,
            string[] preferredLocations,
            string[] contractTypes,
            string[] workModes,
            decimal? minimumAnnualGrossSalary,
            string? salaryCurrency,
            DateOnly? availableFrom)
        {
            if (Preferences is null)
            {
                Preferences = new CandidatePreferences(Id, desiredJobTitles, preferredLocations, contractTypes, workModes, minimumAnnualGrossSalary, salaryCurrency, availableFrom);
            }
            else
            {
                Preferences.Update(desiredJobTitles, preferredLocations, contractTypes, workModes, minimumAnnualGrossSalary, salaryCurrency, availableFrom);
            }

            UpdatedAtUtc = DateTime.UtcNow;
            return Preferences;
        }

        public bool RemovePreferences()
        {
            if (Preferences is null)
                return false;

            Preferences = null;
            UpdatedAtUtc = DateTime.UtcNow;
            return true;
        }
    }
}
