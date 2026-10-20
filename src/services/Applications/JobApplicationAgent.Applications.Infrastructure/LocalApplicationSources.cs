using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Applications.Infrastructure;

public sealed record ApplicationSourceBinding(Uri JobOrigin, Uri ProfileOrigin)
{
    public static ApplicationSourceBinding From(IConfiguration config)
    {
        return new(Origin(config["Applications:Sources:JobBaseUrl"] ?? "http://localhost:5002", "job-api"),
            Origin(config["Applications:Sources:ProfileBaseUrl"] ?? "http://localhost:5001", "profile-api"));
    }
    private static Uri Origin(string value, string allowedComposeHost)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new InvalidOperationException("Application source URLs must be loopback origins or the matching local Compose service origin.");
        var isApprovedComposeOrigin = uri.Scheme == "http" && uri.Port == 8080 &&
            string.Equals(uri.Host, allowedComposeHost, StringComparison.OrdinalIgnoreCase);
        if (!uri.IsLoopback && !isApprovedComposeOrigin)
            throw new InvalidOperationException("Application source URLs must be loopback origins or the matching local Compose service origin.");
        return uri;
    }
}

public sealed class LocalApplicationSources(HttpClient http, ApplicationSourceBinding binding) : IApplicationSources
{
    public async Task<VerifiedApplicationSources> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, CancellationToken cancellationToken)
    {
        if (ownerId == Guid.Empty || profileId == Guid.Empty || jobId == Guid.Empty)
            throw new ApplicationSourceRejectedException("Owner, profile and job identifiers are required.");
        try
        {
            var profile = await ReadAsync<ProfileIdentity>(new(binding.ProfileOrigin, "api/v1/profile"), cancellationToken);
            if (profile.Id != profileId) throw new ApplicationSourceRejectedException("The profile does not belong to the authenticated owner.");
            var job = await ReadAsync<JobSource>(new(binding.JobOrigin, $"api/v1/jobs/{jobId}"), cancellationToken);
            if (job.Id != jobId || !ValidText(job.Title) || !ValidText(job.CompanyName) || job.Status is not ("Discovered" or "Saved" or "Ignored" or "Expired"))
                throw new ApplicationSourceUnavailableException();
            return new(ownerId, profileId, jobId, job.Title!, job.CompanyName!, job.Status is "Discovered" or "Saved");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        { throw new ApplicationSourceUnavailableException(); }
    }
    private async Task<T> ReadAsync<T>(Uri uri, CancellationToken ct)
    {
        // Origins are restricted to loopback; the handler forwards only the validated incoming bearer token.
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new ApplicationSourceRejectedException("A required source no longer exists.");
        if (response.StatusCode != HttpStatusCode.OK) throw new ApplicationSourceUnavailableException();
        await response.Content.LoadIntoBufferAsync(131072, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw new ApplicationSourceUnavailableException();
    }
    private static bool ValidText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    private sealed record ProfileIdentity(Guid Id);
    private sealed record JobSource(Guid Id, string? Title, string? CompanyName, string? Status);
}
