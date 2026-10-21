using FluentValidation;
using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed record PublicationReceipt(Guid OfferId, Guid RevisionId, Guid JobId, string Fingerprint, DateTime PublishedAtUtc);
public sealed class PublicationDependencyException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
public interface IJobPublicationClient
{
    Task<PublicationReceipt?> GetAsync(Guid offerId, CancellationToken ct);
    Task<PublicationReceipt> PublishAsync(OfferRevision revision, CancellationToken ct);
}

public sealed class PublishOfferHandler(IOfferRevisionRepository revisions, IJobPublicationClient jobs, TimeProvider clock)
{
    public async Task<PublicationReceipt?> HandleAsync(Guid offerId, Guid revisionId, CancellationToken ct)
    {
        if (offerId == Guid.Empty || revisionId == Guid.Empty) throw new ValidationException("IDs cannot be empty.");
        var revision = await revisions.GetAsync(offerId, revisionId, ct);
        if (revision is null) return null;
        if (revision.Decision != "Approved") throw new RevisionConflictException("Only an approved revision can be published.");
        // The receiver is authoritative: this recovers a committed publication even after expiry or a lost response.
        var receipt = await jobs.GetAsync(offerId, ct);
        if (receipt is not null) return Match(receipt, revision);
        if (revision.ReadContent().ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
            throw new RevisionConflictException("An expired revision cannot be published.");
        return Match(await jobs.PublishAsync(revision, ct), revision);
    }
    private static PublicationReceipt Match(PublicationReceipt receipt, OfferRevision revision)
    {
        if (receipt.OfferId != revision.OfferId || receipt.RevisionId != revision.Id || receipt.Fingerprint != revision.Fingerprint)
            throw new RevisionConflictException("This import has already been published with another revision.");
        return receipt;
    }
}
