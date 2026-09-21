using JobApplicationAgent.Profile.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace JobApplicationAgent.Profile.Application.Abstractions
{
    public interface ICandidateProfileRepository
    {
        Task<CandidateProfile?> GetAsync(CancellationToken cancellationToken = default);

        Task AddAsync(CandidateProfile profile, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<CandidateProfile?> GetForUpdateAsync(CancellationToken cancellationToken = default);
    }
}
