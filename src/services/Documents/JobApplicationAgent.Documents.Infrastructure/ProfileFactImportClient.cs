using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ProfileFactImportClient(HttpClient http, IConfiguration configuration) : IProfileFactImportClient
{
    public async Task<ProfileFactReceipt> ImportAsync(ResumeCandidateFact fact, CancellationToken ct)
    {
        var token = configuration["Documents:ProfileImport:Token"];
        var address = configuration["Documents:ProfileImport:BaseUrl"] ?? "http://localhost:5001";
        if (!Guid.TryParse(configuration["Documents:ProfileImport:ProfileId"], out var target) || target == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace) ||
            !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new ProfileFactImportUnavailableException();
        try
        {
            using var lookup = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, $"api/v1/profile/document-facts/{fact.Id}"));
            lookup.Headers.Authorization = new("Bearer", token);
            using var existing = await http.SendAsync(lookup, HttpCompletionOption.ResponseHeadersRead, ct);
            if (existing.StatusCode == HttpStatusCode.OK) return await ReadReceipt(existing, fact, target, ct);
            if (existing.StatusCode != HttpStatusCode.NotFound) throw new ProfileFactImportUnavailableException();
            var dto = CandidateFactDto.From(fact);
            var payload = new Dictionary<string, object?>
            {
                ["FactId"] = fact.Id, ["OwnerId"] = fact.OwnerId, ["TargetProfileId"] = target,
                ["ResumeId"] = fact.ResumeId, ["ExtractionId"] = fact.ExtractionId, ["SourceSha256"] = fact.SourceSha256,
                ["EngineVersion"] = fact.EngineVersion, ["Kind"] = fact.Kind, ["Decision"] = fact.Decision,
                ["ReviewedAtUtc"] = fact.ReviewedAtUtc, ["ReviewedByOwnerId"] = fact.ReviewedByOwnerId, ["Fields"] = dto.Fields
            };
            if (fact.Kind != "Skill")
            {
                var dates = System.Text.Json.JsonSerializer.Deserialize<ConfirmProfileDatesCommand>(fact.ProfileDatesJson!)!;
                payload["Dates"] = new { dates.StartDate, dates.EndDate, dates.IsCurrent, dates.IssueDate, dates.ExpirationDate,
                    dates.Confirmed, dates.ConfirmationNote, ConfirmedByOwnerId = fact.ProfileDatesConfirmedByOwnerId,
                    ConfirmedAtUtc = fact.ProfileDatesConfirmedAtUtc };
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(uri, "api/v1/profile/document-facts"));
            request.Headers.Authorization = new("Bearer", token);
            request.Content = JsonContent.Create(payload);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
                throw new ProfileFactImportRejectedException("Profile could not validate or reconcile this fact. Check its mapping, binding and existing records.");
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created)) throw new ProfileFactImportUnavailableException();
            return await ReadReceipt(response, fact, target, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException ||
            exception is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new ProfileFactImportUnavailableException(); }
    }
    private static async Task<ProfileFactReceipt> ReadReceipt(HttpResponseMessage response, ResumeCandidateFact fact, Guid target, CancellationToken ct)
    {
        await response.Content.LoadIntoBufferAsync(16384, ct);
        var receipt = await response.Content.ReadFromJsonAsync<ProfileFactReceipt>(ct);
        if (receipt is null || receipt.FactId != fact.Id || receipt.OwnerId != fact.OwnerId || receipt.ProfileId != target ||
            receipt.SourceSha256 != fact.SourceSha256 || receipt.Kind != fact.Kind ||
            (receipt.EntityId == Guid.Empty && (fact.Kind != "Skill" || receipt.SkillId == Guid.Empty)) ||
            (fact.Kind == "Skill" && receipt.EntityId != Guid.Empty && receipt.SkillId != receipt.EntityId) ||
            (fact.Kind != "Skill" && receipt.SkillId != Guid.Empty) ||
            receipt.Action is not ("Created" or "LinkedExisting") || receipt.ImportedAtUtc.Kind != DateTimeKind.Utc)
            throw new ProfileFactImportUnavailableException();
        return receipt;
    }
}
