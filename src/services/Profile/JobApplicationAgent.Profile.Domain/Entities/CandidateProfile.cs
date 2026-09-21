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
    }
}


