namespace JobApplicationAgent.Matching.Application.Models;

public sealed record JobMatchingData(
    Guid Id,
    string Title,
    string CompanyName,
    string? Location,
    string WorkMode,
    string ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string? Description);
