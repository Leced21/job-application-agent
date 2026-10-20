namespace JobApplicationAgent.Collection.Application.Imports;

public sealed record ImportOfferCommand(
    string Title, string CompanyName, string? Location = null, string? WorkMode = null,
    string? ContractType = null, decimal? SalaryMin = null, decimal? SalaryMax = null,
    string? SalaryCurrency = null, string? Description = null, string[]? Skills = null,
    string? SourceName = null, string? SourceUrl = null, DateTime? PublishedAtUtc = null, DateTime? ExpiresAtUtc = null);
