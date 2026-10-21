using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;

public sealed class MatchingResultRepository(MatchingDbContext dbContext) : IMatchingResultRepository
{
    public async Task AddAsync(MatchingResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        var record = new MatchingResultRecord
        {
            Id = result.Id,
            CandidateProfileId = result.CandidateProfileId,
            JobId = result.JobId,
            Status = result.Status.ToString(),
            ScoringVersion = result.ScoringVersion,
            OverallScore = result.OverallScore,
            EvaluationCoverage = result.EvaluationCoverage,
            CreatedAtUtc = result.CreatedAtUtc,
            CompletedAtUtc = result.CompletedAtUtc,

            Criteria = result.Criteria.Select(criterion =>
                new MatchingCriterionRecord
                {
                    Id = Guid.NewGuid(),
                    MatchingResultId = result.Id,
                    Name = criterion.Name,
                    Score = criterion.Score,
                    Weight = criterion.Weight,
                    Explanation = criterion.Explanation,
                    Status = criterion.Status.ToString()
                }).ToList()
        };

        await dbContext.MatchingResults.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MatchingResultDetails?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Matching result ID is required.",
                nameof(id));
        }

        return await dbContext.MatchingResults
            .AsNoTracking()
            .Where(result => result.Id == id)
            .Select(result => new MatchingResultDetails(
                result.Id,
                result.CandidateProfileId,
                result.JobId,
                result.Status,
                result.OverallScore,
                result.EvaluationCoverage,
                result.Criteria
                    .OrderBy(criterion => criterion.Name)
                    .Select(criterion => new MatchingCriterionDetails(
                        criterion.Name,
                        criterion.Score,
                        criterion.Weight,
                        criterion.Status,
                        criterion.Explanation))
                    .ToArray(),
                result.CreatedAtUtc,
                result.CompletedAtUtc)
            {
                ScoringVersion = result.ScoringVersion
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<MatchingResultPage> SearchAsync(
        MatchingResultFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                "Page must be greater than zero.");
        }

        if (filter.PageSize < 1 || filter.PageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                "Page size must be between 1 and 100.");
        }

        var query = dbContext.MatchingResults
            .AsNoTracking()
            .AsQueryable();

        if (filter.CandidateProfileId.HasValue)
        {
            query = query.Where(result =>
                result.CandidateProfileId == filter.CandidateProfileId.Value);
        }

        if (filter.JobId.HasValue)
        {
            query = query.Where(result =>
                result.JobId == filter.JobId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(result =>
                result.Status == filter.Status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(result => result.CreatedAtUtc)
            .ThenByDescending(result => result.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(result => new MatchingResultSummary(
                result.Id,
                result.CandidateProfileId,
                result.JobId,
                result.Status,
                result.OverallScore,
                result.EvaluationCoverage,
                result.CreatedAtUtc,
                result.CompletedAtUtc)
            {
                ScoringVersion = result.ScoringVersion
            })
            .ToArrayAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            (double)totalCount / filter.PageSize);

        return new MatchingResultPage(
            items,
            totalCount,
            filter.Page,
            filter.PageSize,
            totalPages);
    }
}
