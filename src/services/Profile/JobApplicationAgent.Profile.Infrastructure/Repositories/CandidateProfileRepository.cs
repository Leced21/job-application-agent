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
        public Task<CandidateProfile?> GetForUpdateAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.CandidateProfiles
                            .Include(x => x.ProfessionalExperiences)
                            .Include(x => x.Educations)
                            .Include(x => x.Skills)
                            .SingleOrDefaultAsync(cancellationToken);
        }
        public Task<CandidateProfile?> GetWithProfessionalExperiencesAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.CandidateProfiles
                            .AsNoTracking()
                            .Include(x => x.ProfessionalExperiences)
                            .SingleOrDefaultAsync(cancellationToken);
        }
        public Task<CandidateProfile?> GetWithEducationsAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.CandidateProfiles
                            .AsNoTracking()
                            .Include(x => x.Educations)
                            .SingleOrDefaultAsync(cancellationToken);
        }
        public Task<CandidateProfile?> GetWithSkillsAsync(CancellationToken cancellationToken = default)
        {
            return dbContext.CandidateProfiles
                .AsNoTracking()
                .Include(x => x.Skills)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
