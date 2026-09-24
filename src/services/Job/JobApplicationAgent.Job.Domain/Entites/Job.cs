using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.Domain.Entities;

public sealed class Job
{
    private Job()
    {
    }

    private Job(
        Guid id,
        string title,
        string companyName,
        string? location,
        WorkMode workMode,
        ContractType contractType,
        decimal? salaryMin,
        decimal? salaryMax,
        string? salaryCurrency,
        string? description,
        string source,
        string? sourceUrl,
        DateTime? publishedAtUtc,
        DateTime createdAtUtc)
    {
        Id = id;
        Title = title;
        CompanyName = companyName;
        Location = location;
        WorkMode = workMode;
        ContractType = contractType;
        SalaryMin = salaryMin;
        SalaryMax = salaryMax;
        SalaryCurrency = salaryCurrency;
        Description = description;
        Source = source;
        SourceUrl = sourceUrl;
        Status = JobStatus.Discovered;
        PublishedAtUtc = publishedAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string CompanyName { get; private set; } = null!;

    public string? Location { get; private set; }

    public WorkMode WorkMode { get; private set; }

    public ContractType ContractType { get; private set; }

    public decimal? SalaryMin { get; private set; }

    public decimal? SalaryMax { get; private set; }

    public string? SalaryCurrency { get; private set; }

    public string? Description { get; private set; }

    public string Source { get; private set; } = null!;

    public string? SourceUrl { get; private set; }

    public JobStatus Status { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static Job Create(
        string title,
        string companyName,
        string? location,
        WorkMode workMode,
        ContractType contractType,
        decimal? salaryMin,
        decimal? salaryMax,
        string? salaryCurrency,
        string? description,
        string source,
        string? sourceUrl,
        DateTime? publishedAtUtc)
    {
        var now = DateTime.UtcNow;

        return new Job(
            Guid.NewGuid(),
            title.Trim(),
            companyName.Trim(),
            Normalize(location),
            workMode,
            contractType,
            salaryMin,
            salaryMax,
            Normalize(salaryCurrency),
            Normalize(description),
            source.Trim(),
            Normalize(sourceUrl),
            publishedAtUtc,
            now);
    }

    public void Update(
        string title,
        string companyName,
        string? location,
        WorkMode workMode,
        ContractType contractType,
        decimal? salaryMin,
        decimal? salaryMax,
        string? salaryCurrency,
        string? description,
        string source,
        string? sourceUrl,
        DateTime? publishedAtUtc)
    {
        Title = title.Trim();
        CompanyName = companyName.Trim();
        Location = Normalize(location);
        WorkMode = workMode;
        ContractType = contractType;
        SalaryMin = salaryMin;
        SalaryMax = salaryMax;
        SalaryCurrency = Normalize(salaryCurrency);
        Description = Normalize(description);
        Source = source.Trim();
        SourceUrl = Normalize(sourceUrl);
        PublishedAtUtc = publishedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeStatus(JobStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}