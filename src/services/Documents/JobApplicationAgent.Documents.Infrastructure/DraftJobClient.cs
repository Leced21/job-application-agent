using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Documents.Application;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class DraftJobClient(HttpClient http, IConfiguration config) : IDraftJobClient
{
    public async Task<DraftJobSnapshot?> GetAsync(Guid jobId, CancellationToken ct)
    {
        var address = config["Documents:Drafts:JobBaseUrl"] ?? "http://localhost:5002";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/" ||
            !uri.IsLoopback && !(uri.Scheme == "http" && uri.Port == 8080 && string.Equals(uri.Host, "job-api", StringComparison.OrdinalIgnoreCase)))
            throw new DraftJobUnavailableException();
        try
        {
            using var response = await http.GetAsync(new Uri(uri, $"api/v1/jobs/{jobId}"), HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode != HttpStatusCode.OK) throw new DraftJobUnavailableException();
            await response.Content.LoadIntoBufferAsync(131072, ct);
            var job = await response.Content.ReadFromJsonAsync<DraftJobSnapshot>(ct);
            if (job is null || job.Id != jobId || string.IsNullOrWhiteSpace(job.Title) || job.Title.Length > 200 ||
                string.IsNullOrWhiteSpace(job.CompanyName) || job.CompanyName.Length > 200 || job.Location?.Length > 200 ||
                job.Description?.Length > 10000 || string.IsNullOrWhiteSpace(job.Source) || job.Source.Length > 100 || job.SourceUrl?.Length > 2000 ||
                !string.IsNullOrEmpty(job.SourceUrl) && (!Uri.TryCreate(job.SourceUrl, UriKind.Absolute, out var sourceUri) || sourceUri.Scheme is not ("http" or "https")) ||
                job.Status is not ("Discovered" or "Saved" or "Ignored" or "Expired") || job.UpdatedAtUtc == default || job.UpdatedAtUtc.Kind != DateTimeKind.Utc)
                throw new DraftJobUnavailableException();
            return job;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException ||
            exception is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new DraftJobUnavailableException(); }
    }
}
