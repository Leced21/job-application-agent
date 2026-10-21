using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Infrastructure;

public sealed class JobPublicationClient(HttpClient http) : IJobPublicationClient
{
    private const string Route = "api/v1/jobs/collection-publications";
    public Task<PublicationReceipt?> GetAsync(Guid offerId, CancellationToken ct) => SendAsync(
        new HttpRequestMessage(HttpMethod.Get, $"{Route}/{offerId}"), offerId, true, ct);

    public async Task<PublicationReceipt> PublishAsync(OfferRevision revision, CancellationToken ct)
    {
        var content = revision.ReadContent();
        return (await SendAsync(new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = JsonContent.Create(new
            {
                revision.OfferId, RevisionId = revision.Id, Fingerprint = revision.Fingerprint,
                Job = new { content.Title, content.CompanyName, content.Location, content.WorkMode, content.ContractType,
                    content.SalaryMin, content.SalaryMax, content.SalaryCurrency, content.Description,
                    Source = content.SourceName, content.SourceUrl, content.PublishedAtUtc },
                content.Skills, content.ExpiresAtUtc
            })
        }, revision.OfferId, false, ct))!;
    }

    private async Task<PublicationReceipt?> SendAsync(HttpRequestMessage request, Guid offerId, bool lookup, CancellationToken ct)
    {
        using (request)
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (lookup && response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new RevisionConflictException("Job rejected a conflicting or expired publication.");
            if (response.StatusCode == HttpStatusCode.BadRequest)
                throw new PublicationDependencyException(502, "Job rejected the publication contract.");
            if ((lookup && response.StatusCode != HttpStatusCode.OK) ||
                (!lookup && response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created)))
                throw new PublicationDependencyException(503, "Job publication service is unavailable.");
            var receipt = await response.Content.ReadFromJsonAsync<PublicationReceipt>(ct);
            if (receipt is null || receipt.OfferId != offerId || receipt.JobId == Guid.Empty || receipt.RevisionId == Guid.Empty ||
                receipt.Fingerprint?.Length != 64 || receipt.PublishedAtUtc.Kind != DateTimeKind.Utc || receipt.PublishedAtUtc == default)
                throw new PublicationDependencyException(502, "Job returned an invalid publication receipt.");
            return receipt;
        }
        catch (JsonException) { throw new PublicationDependencyException(502, "Job returned an invalid publication receipt."); }
        catch (HttpRequestException) { throw new PublicationDependencyException(503, "Job publication service is unavailable."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new PublicationDependencyException(503, "Job publication request timed out; retry the same revision."); }
    }
}
