using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.Application.Jobs;

public sealed record JobDto(
    Guid Id,
    string Title,
    string CompanyName,
    string? Location,
    WorkMode WorkMode,
    ContractType ContractType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string? Description,
    string Source,
    string? SourceUrl,
    JobStatus Status,
    DateTime? PublishedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
    