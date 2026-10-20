
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Infrastructure.HttpClients;

public sealed class JobMatchingHttpClient(HttpClient httpClient)
    : IJobMatchingDataClient
{
    public async Task<JobMatchingData?> GetByIdAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job ID is required.",
                nameof(jobId));
        }

        using var response = await httpClient.GetAsync(
            $"api/v1/jobs/{jobId}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JobMatchingData>(
            cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<JobMatchingData>> GetPageAsync(
        int page, int pageSize, CancellationToken cancellationToken = default) =>
        FetchPageAsync("api/v1/jobs", page, pageSize, cancellationToken);

    public async Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var jobs = await FetchPageAsync("api/v1/jobs/recommendable", page, pageSize, cancellationToken);

        if (jobs.Count > pageSize || jobs.Any(job =>
            job is null || job.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(job.Title) ||
            string.IsNullOrWhiteSpace(job.CompanyName) ||
            string.IsNullOrWhiteSpace(job.WorkMode) ||
            string.IsNullOrWhiteSpace(job.ContractType)))
        {
            throw new JsonException("Job Service returned an invalid recommendable jobs response.");
        }

        return jobs;
    }

    private async Task<IReadOnlyList<JobMatchingData>> FetchPageAsync(
        string endpoint, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || page > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "Page must be between 1 and 1000000.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "Page size must be between 1 and 100.");
        }

        using var response = await httpClient.GetAsync(
            $"{endpoint}?page={page}&pageSize={pageSize}", cancellationToken);

        response.EnsureSuccessStatusCode();

        var jobs = await response.Content.ReadFromJsonAsync<List<JobMatchingData>>(cancellationToken: cancellationToken);

        return jobs ?? throw new JsonException("Job Service returned an invalid jobs response.");
    }

}
