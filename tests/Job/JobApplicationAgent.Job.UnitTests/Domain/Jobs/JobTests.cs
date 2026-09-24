using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;

namespace JobApplicationAgent.Job.UnitTests.Domain.Jobs;

public sealed class JobTests
{
    [Fact]
    public void Create_ShouldCreateJobWithExpectedValues()
    {
        var publishedAtUtc = DateTime.UtcNow.AddDays(-1);

        var job = JobEntity.Create(
            title: "Data Engineer",
            companyName: "OpenAI",
            location: "Paris",
            workMode: WorkMode.Hybrid,
            contractType: ContractType.Permanent,
            salaryMin: 55_000m,
            salaryMax: 70_000m,
            salaryCurrency: "EUR",
            description: "Build data platforms.",
            source: "CompanyWebsite",
            sourceUrl: "https://example.com/jobs/123",
            publishedAtUtc: publishedAtUtc);

        Assert.NotEqual(Guid.Empty, job.Id);

        Assert.Equal("Data Engineer", job.Title);
        Assert.Equal("OpenAI", job.CompanyName);
        Assert.Equal("Paris", job.Location);

        Assert.Equal(WorkMode.Hybrid, job.WorkMode);
        Assert.Equal(ContractType.Permanent, job.ContractType);

        Assert.Equal(55_000m, job.SalaryMin);
        Assert.Equal(70_000m, job.SalaryMax);
        Assert.Equal("EUR", job.SalaryCurrency);

        Assert.Equal(
            "Build data platforms.",
            job.Description);

        Assert.Equal("CompanyWebsite", job.Source);

        Assert.Equal(
            "https://example.com/jobs/123",
            job.SourceUrl);

        Assert.Equal(
            JobStatus.Discovered,
            job.Status);

        Assert.Equal(
            publishedAtUtc,
            job.PublishedAtUtc);

        Assert.NotEqual(default, job.CreatedAtUtc);

        Assert.Equal(
            job.CreatedAtUtc,
            job.UpdatedAtUtc);
    }

    [Fact]
    public void Create_ShouldTrimTextValues()
    {
        var job = JobEntity.Create(
            title: "  Data Engineer  ",
            companyName: "  Test Company  ",
            location: "  Paris  ",
            workMode: WorkMode.Remote,
            contractType: ContractType.Permanent,
            salaryMin: null,
            salaryMax: null,
            salaryCurrency: "  EUR  ",
            description: "  Description  ",
            source: "  LinkedIn  ",
            sourceUrl: "  https://example.com/job  ",
            publishedAtUtc: null);

        Assert.Equal("Data Engineer", job.Title);
        Assert.Equal("Test Company", job.CompanyName);
        Assert.Equal("Paris", job.Location);
        Assert.Equal("EUR", job.SalaryCurrency);
        Assert.Equal("Description", job.Description);
        Assert.Equal("LinkedIn", job.Source);

        Assert.Equal(
            "https://example.com/job",
            job.SourceUrl);
    }

    [Fact]
    public void Create_ShouldConvertOptionalWhitespaceValuesToNull()
    {
        var job = JobEntity.Create(
            title: "Data Engineer",
            companyName: "Test Company",
            location: "   ",
            workMode: WorkMode.Unknown,
            contractType: ContractType.Unknown,
            salaryMin: null,
            salaryMax: null,
            salaryCurrency: " ",
            description: "",
            source: "Manual",
            sourceUrl: "   ",
            publishedAtUtc: null);

        Assert.Null(job.Location);
        Assert.Null(job.SalaryCurrency);
        Assert.Null(job.Description);
        Assert.Null(job.SourceUrl);
        Assert.Null(job.PublishedAtUtc);
    }

    [Fact]
    public void Update_ShouldUpdateJob()
    {
        var job = CreateJob();

        var previousUpdatedAtUtc =
            job.UpdatedAtUtc;

        job.Update(
            title: "Senior Data Engineer",
            companyName: "Updated Company",
            location: "Lyon",
            workMode: WorkMode.Remote,
            contractType: ContractType.Freelance,
            salaryMin: 65_000m,
            salaryMax: 80_000m,
            salaryCurrency: "EUR",
            description: "Updated description",
            source: "UpdatedSource",
            sourceUrl: "https://example.com/updated",
            publishedAtUtc: DateTime.UtcNow);

        Assert.Equal(
            "Senior Data Engineer",
            job.Title);

        Assert.Equal(
            "Updated Company",
            job.CompanyName);

        Assert.Equal("Lyon", job.Location);
        Assert.Equal(WorkMode.Remote, job.WorkMode);

        Assert.Equal(
            ContractType.Freelance,
            job.ContractType);

        Assert.Equal(65_000m, job.SalaryMin);
        Assert.Equal(80_000m, job.SalaryMax);

        Assert.True(
            job.UpdatedAtUtc >= previousUpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_ShouldChangeStatus()
    {
        var job = CreateJob();

        job.ChangeStatus(
            JobStatus.Saved);

        Assert.Equal(
            JobStatus.Saved,
            job.Status);
    }

    private static JobEntity CreateJob()
    {
        return JobEntity.Create(
            title: "Data Engineer",
            companyName: "Test Company",
            location: "Paris",
            workMode: WorkMode.Hybrid,
            contractType: ContractType.Permanent,
            salaryMin: 55_000m,
            salaryMax: 70_000m,
            salaryCurrency: "EUR",
            description: "Description",
            source: "Manual",
            sourceUrl: null,
            publishedAtUtc: null);
    }
}