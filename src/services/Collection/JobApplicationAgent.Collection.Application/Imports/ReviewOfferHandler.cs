using FluentValidation;
using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed record OfferRevisionDto(Guid Id, Guid OfferId, string Fingerprint, string OriginalTitle,
    OfferContent Content, string NormalizationVersion, DateTime CollectedAtUtc, string Decision, DateTime? ReviewedAtUtc,
    Guid? ReviewedByOwnerId)
{
    public static OfferRevisionDto From(OfferRevision revision) => new(revision.Id, revision.OfferId,
        revision.Fingerprint, revision.OriginalTitle, revision.ReadContent(), revision.NormalizationVersion,
        revision.CollectedAtUtc, revision.Decision, revision.ReviewedAtUtc, revision.ReviewedByOwnerId);
}

public sealed record ReviewOfferCommand(string Decision);

public sealed class ReviewOfferHandler(IOfferRevisionRepository repository, TimeProvider clock)
{
    public async Task<IReadOnlyList<OfferRevisionDto>> GetPageAsync(Guid offerId, int page, int pageSize, CancellationToken ct)
    {
        ValidateId(offerId);
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            throw new ValidationException("Page must be 1–1000000 and pageSize 1–100.");
        return (await repository.GetPageAsync(offerId, page, pageSize, ct)).Select(OfferRevisionDto.From).ToArray();
    }
    public async Task<OfferRevisionDto?> ReviewAsync(Guid offerId, Guid revisionId, ReviewOfferCommand command,
        Guid reviewerOwnerId, CancellationToken ct)
    {
        ValidateId(offerId);
        ValidateId(revisionId);
        ValidateId(reviewerOwnerId);
        if (command.Decision is not ("Approved" or "Rejected"))
            throw new ValidationException("Decision must be Approved or Rejected.");
        var revision = await repository.ReviewAsync(offerId, revisionId, command.Decision,
            clock.GetUtcNow().UtcDateTime, reviewerOwnerId, ct);
        return revision is null ? null : OfferRevisionDto.From(revision);
    }
    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new ValidationException("ID cannot be empty.");
    }
}
