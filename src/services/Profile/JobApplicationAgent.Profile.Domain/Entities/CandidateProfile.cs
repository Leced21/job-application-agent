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
        public string Summary { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }


    }
}
