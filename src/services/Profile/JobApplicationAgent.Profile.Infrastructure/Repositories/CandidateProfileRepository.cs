using JobApplicationAgent.Profile.Application.Abstractions;
using JobApplicationAgent.Profile.Domain.Entities;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Profile.Infrastructure.Repositories
{
    public sealed class CandidateProfileRepository(ProfileDbContext dbContext) : ICandidateProfileRepository
    {
        public Task<CandidateProfile?> GetAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.CandidateProfiles
                            .AsNoTracking()
                            .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task AddAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
        {
            await dbContext.CandidateProfiles.AddAsync(profile, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
