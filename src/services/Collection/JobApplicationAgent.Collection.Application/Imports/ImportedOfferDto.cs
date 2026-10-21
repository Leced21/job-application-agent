using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed record ImportedOfferDto(Guid Id, string OriginalTitle, string Title, string CompanyName, string? Location,
    string WorkMode, string ContractType, decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency,
    string? Description, string[] Skills, string SourceName, string? SourceUrl, DateTime? PublishedAtUtc,
    DateTime? ExpiresAtUtc, DateTime FirstCollectedAtUtc, DateTime LastSeenAtUtc, long SeenCount,
    string Status, string NormalizationVersion, bool HasPendingChanges)
{
    public static ImportedOfferDto From(CollectedOffer offer, DateTime now) => new(offer.Id, offer.OriginalTitle,
        offer.Title, offer.CompanyName, offer.Location, offer.WorkMode, offer.ContractType, offer.SalaryMin,
        offer.SalaryMax, offer.SalaryCurrency, offer.Description, offer.Skills.ToArray(), offer.SourceName,
        offer.SourceUrl, offer.PublishedAtUtc, offer.ExpiresAtUtc, offer.FirstCollectedAtUtc, offer.LastSeenAtUtc,
        offer.SeenCount, offer.IsExpired(now) ? "Expired" : "PendingReview", offer.NormalizationVersion, offer.HasPendingChanges);
}
public sealed record ImportOfferResponse(ImportedOfferDto Offer, bool IsDuplicate, bool ContentChanged);
public sealed record ImportedOfferPage(IReadOnlyList<ImportedOfferDto> Items, int TotalCount, int Page, int PageSize, int TotalPages);
