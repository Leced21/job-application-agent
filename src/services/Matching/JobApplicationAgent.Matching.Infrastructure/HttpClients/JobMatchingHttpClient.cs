
using System.Net;
using System.Net.Http.Json;
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
}
