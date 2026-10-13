using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Infrastructure.HttpClients;

public sealed class ProfileMatchingHttpClient(HttpClient httpClient)
    : IProfileMatchingDataClient
{
    public async Task<ProfileMatchingData?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            "api/v1/profile/matching-data",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ProfileMatchingData>(cancellationToken: cancellationToken);
    }
}
