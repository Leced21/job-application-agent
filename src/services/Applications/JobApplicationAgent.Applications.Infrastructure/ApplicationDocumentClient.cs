using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Applications.Infrastructure;

public sealed class ApplicationDocumentClient(HttpClient http, IConfiguration config) : IApplicationDocumentClient
{
    public async Task<ApplicationDocument> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, Guid exportId, CancellationToken ct)
    {
        if (ownerId == Guid.Empty || profileId == Guid.Empty || jobId == Guid.Empty || exportId == Guid.Empty ||
            !Uri.TryCreate(config["Applications:Documents:BaseUrl"] ?? "http://localhost:5005", UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https") || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0 ||
            !origin.IsLoopback && !(origin.Scheme == "http" && origin.Port == 8080 && string.Equals(origin.Host, "documents-api", StringComparison.OrdinalIgnoreCase)))
            throw new ApplicationSourceUnavailableException();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, $"api/v1/documents/application-bindings/{exportId}"));
            var channelToken = config["Applications:Documents:Token"];
            if (channelToken is null || channelToken.Length is < 32 or > 512 || channelToken.Any(char.IsWhiteSpace))
                throw new ApplicationSourceUnavailableException();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", channelToken);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
                throw new ApplicationSourceRejectedException("Document is absent, unconfirmed or no longer approved.");
            if (response.StatusCode != HttpStatusCode.OK) throw new ApplicationSourceUnavailableException();
            await response.Content.LoadIntoBufferAsync(16384, ct);
            var receipt = await response.Content.ReadFromJsonAsync<Receipt>(ct);
            if (receipt is null || receipt.ExportId != exportId || receipt.OwnerId != ownerId || receipt.ProfileId != profileId || receipt.JobId != jobId ||
                receipt.DraftId == Guid.Empty || receipt.Kind is not ("Resume" or "CoverLetter") || !Hash(receipt.FileSha256) ||
                !Hash(receipt.CompositionSha256) || !Hash(receipt.SnapshotSha256) || receipt.ConfirmedAtUtc == default || receipt.ConfirmedAtUtc.Kind != DateTimeKind.Utc)
                throw new ApplicationSourceRejectedException("Document receipt does not match the dossier.");
            return new(exportId, ownerId, profileId, jobId, Enum.Parse<ApplicationDocumentKind>(receipt.Kind), receipt.FileSha256!);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new ApplicationSourceUnavailableException(); }
    }
    private static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    private sealed record Receipt(Guid ExportId, Guid OwnerId, Guid ProfileId, Guid JobId, Guid DraftId, string? Kind,
        string? FileSha256, string? CompositionSha256, string? SnapshotSha256, DateTime ConfirmedAtUtc);
}
