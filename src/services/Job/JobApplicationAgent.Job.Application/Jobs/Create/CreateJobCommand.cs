using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.Application.Jobs.Create;

public sealed record CreateJobCommand(
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
    DateTime? PublishedAtUtc);