using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Job.Application.Jobs;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Application.Jobs.Update;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.IntegrationTests.Fixtures;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;

namespace JobApplicationAgent.Job.IntegrationTests.Jobs;

public sealed class JobsEndpointsTests
    : JobIntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public JobsEndpointsTests(JobApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedJob()
    {
        var command = CreateCommand();

        var response = await Client.PostAsJsonAsync(
            "/api/v1/jobs",
            command);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var job =
            await response.Content
                .ReadFromJsonAsync<JobDto>(JsonOptions);

        Assert.NotNull(job);
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(command.Title, job.Title);
        Assert.Equal(
            command.CompanyName,
            job.CompanyName);

        Assert.Equal(
            JobStatus.Discovered,
            job.Status);
    }

    [Fact]
    public async Task GetAll_ShouldReturnCreatedJob()
    {
        var created = await CreateJobAsync();

        var response =
            await Client.GetAsync("/api/v1/jobs");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var jobs =
            await response.Content
                .ReadFromJsonAsync<List<JobDto>>(JsonOptions);

        Assert.NotNull(jobs);

        Assert.Contains(
            jobs,
            job => job.Id == created.Id);
    }

    [Fact]
    public async Task GetById_ShouldReturnJob_WhenItExists()
    {
        var created = await CreateJobAsync();

        var response =
            await Client.GetAsync(
                $"/api/v1/jobs/{created.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var job =
            await response.Content
                .ReadFromJsonAsync<JobDto>(JsonOptions);

        Assert.NotNull(job);
        Assert.Equal(created.Id, job.Id);
        Assert.Equal(created.Title, job.Title);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        var id = Guid.NewGuid();

        var response =
            await Client.GetAsync(
                $"/api/v1/jobs/{id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldPersistChanges()
    {
        var created = await CreateJobAsync();

        var command = new UpdateJobCommand(
            Title: "Senior Data Engineer",
            CompanyName: "Updated Company",
            Location: "Lyon",
            WorkMode: WorkMode.Remote,
            ContractType: ContractType.Freelance,
            SalaryMin: 65_000m,
            SalaryMax: 80_000m,
            SalaryCurrency: "EUR",
            Description: "Updated description",
            Source: "CompanyWebsite",
            SourceUrl: "https://example.com/jobs/updated",
            PublishedAtUtc: DateTime.UtcNow);

        var updateResponse =
            await Client.PutAsJsonAsync(
                $"/api/v1/jobs/{created.Id}",
                command);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var getResponse =
            await Client.GetAsync(
                $"/api/v1/jobs/{created.Id}");

        var updated =
            await getResponse.Content
                .ReadFromJsonAsync<JobDto>(JsonOptions);

        Assert.NotNull(updated);

        Assert.Equal(
            "Senior Data Engineer",
            updated.Title);

        Assert.Equal("Lyon", updated.Location);
        Assert.Equal(WorkMode.Remote, updated.WorkMode);

        Assert.Equal(
            ContractType.Freelance,
            updated.ContractType);

        Assert.Equal(65_000m, updated.SalaryMin);
        Assert.Equal(80_000m, updated.SalaryMax);

        Assert.True(
            updated.UpdatedAtUtc >= created.UpdatedAtUtc);
    }

    [Fact]
    public async Task Delete_ShouldRemoveJob()
    {
        var created = await CreateJobAsync();

        var deleteResponse =
            await Client.DeleteAsync(
                $"/api/v1/jobs/{created.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            deleteResponse.StatusCode);

        var getResponse =
            await Client.GetAsync(
                $"/api/v1/jobs/{created.Id}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            getResponse.StatusCode);
    }

    private async Task<JobDto> CreateJobAsync()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/jobs",
            CreateCommand());

        response.EnsureSuccessStatusCode();

        var job =
            await response.Content
                .ReadFromJsonAsync<JobDto>(JsonOptions);

        return Assert.IsType<JobDto>(job);
    }

    private static CreateJobCommand CreateCommand()
    {
        return new CreateJobCommand(
            Title: "Data Engineer",
            CompanyName: "Test Company",
            Location: "Paris",
            WorkMode: WorkMode.Hybrid,
            ContractType: ContractType.Permanent,
            SalaryMin: 55_000m,
            SalaryMax: 70_000m,
            SalaryCurrency: "EUR",
            Description: "Integration test job offer",
            Source: "Manual",
            SourceUrl: "https://example.com/jobs/123",
            PublishedAtUtc: DateTime.UtcNow.AddDays(-1));
    }
}