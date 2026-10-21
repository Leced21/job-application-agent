using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.Application.Jobs.Create;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Job.Infrastructure.Repositories;

public sealed class CollectionPublicationRepository(JobDbContext db) : ICollectionPublicationRepository
{
    public Task<CollectionPublication?> GetAsync(Guid offerId, CancellationToken ct) =>
        db.CollectionPublications.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offerId, ct);

    public async Task<PublicationOutcome> PublishAsync(PublishCollectionCommand command, string hash, string json, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize the stable origin identity, including when no receipt row exists yet.
        // Hash collisions only serialize unrelated imports; they never merge their identities.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({command.OfferId.ToString()}, 0))", ct);
        var existing = await GetAsync(command.OfferId, ct);
        if (existing is not null)
        {
            if (existing.RevisionId != command.RevisionId || existing.RequestHash != hash)
                throw new PublicationConflictException("This Collection import has already been published with another revision or payload.");
            await transaction.CommitAsync(ct);
            return new(CollectionPublicationDto.From(existing), false);
        }
        if (command.ExpiresAtUtc <= DateTime.UtcNow)
            throw new PublicationConflictException("An expired revision cannot be published.");
        var content = command.Job;
        var job = Domain.Entities.Job.Create(content.Title, content.CompanyName, content.Location, content.WorkMode,
            content.ContractType, content.SalaryMin, content.SalaryMax, content.SalaryCurrency, content.Description,
            content.Source, content.SourceUrl, content.PublishedAtUtc);
        var receipt = CollectionPublication.Create(command.OfferId, command.RevisionId, job.Id, command.Fingerprint,
            hash, json, DateTime.UtcNow);
        var publishedEvent = JobPublishedV1.CreateMessage(receipt);
        db.Jobs.Add(job);
        db.CollectionPublications.Add(receipt);
        db.OutboxMessages.Add(publishedEvent);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(CollectionPublicationDto.From(receipt), true);
    }
}
