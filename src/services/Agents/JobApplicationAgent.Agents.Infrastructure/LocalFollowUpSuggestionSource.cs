using System.Net;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed class FollowUpSuggestionOptions
{
    private FollowUpSuggestionOptions(bool enabled, Uri applicationsOrigin)
    { Enabled = enabled; ApplicationsOrigin = applicationsOrigin; }
    public bool Enabled { get; }
    public Uri ApplicationsOrigin { get; }
    public static FollowUpSuggestionOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:FollowUpSuggestion:Enabled");
        var originText = config["Agents:FollowUpSuggestion:ApplicationsBaseUrl"] ?? "http://localhost:5007";
        if (!Uri.TryCreate(originText, UriKind.Absolute, out var origin) || !LocalJobSource.IsLocalOrigin(origin, "applications-api") || origin.Scheme is not ("http" or "https") ||
            origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.AbsolutePath != "/")
            throw new ArgumentException("FollowUpSuggestion requires a loopback Applications API origin.");
        return new(enabled, origin);
    }
}

public sealed class LocalFollowUpSuggestionSource(HttpClient http, FollowUpSuggestionOptions options, TimeProvider clock)
{
    private static readonly string[] ActiveStatuses = ["Submitted", "InReview", "Interview"];
    private static readonly string[] Channels = ["Email", "JobBoard", "CompanyWebsite", "Phone", "InPerson", "Other"];

    public async Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid candidacyId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || candidacyId == Guid.Empty) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/applications/{candidacyId:D}");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 262144) throw new InvalidAgentSourceException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        { if (buffer.Length + read > 262144) throw new InvalidAgentSourceException(); buffer.Write(chunk, 0, read); }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 }); RejectDuplicates(document.RootElement);
        var root = document.RootElement;
        if (root.GetProperty("id").GetGuid() != candidacyId || root.GetProperty("ownerId").GetGuid() != owner ||
            root.GetProperty("profileId").GetGuid() == Guid.Empty) return null;
        var status = root.GetProperty("status").GetString(); if (status is null || !ActiveStatuses.Contains(status, StringComparer.Ordinal)) return null;
        var submission = root.GetProperty("submission"); if (submission.ValueKind != JsonValueKind.Object) return null;
        var submittedAt = submission.GetProperty("occurredAtUtc").GetDateTime();
        DateTime? lastUpdate = null;
        foreach (var item in root.GetProperty("events").EnumerateArray())
        {
            if (item.GetProperty("kind").GetString() != "RecruiterUpdateRecorded") continue;
            var update = item.GetProperty("recruiterUpdate"); if (update.ValueKind != JsonValueKind.Object) throw new InvalidAgentSourceException();
            var occurred = update.GetProperty("occurredAtUtc").GetDateTime();
            if (lastUpdate is null || occurred > lastUpdate) lastUpdate = occurred;
        }
        DateTime? pendingDue = null; string? pendingChannel = null;
        var pending = root.GetProperty("pendingFollowUp");
        if (pending.ValueKind == JsonValueKind.Object)
        {
            if (pending.GetProperty("status").GetString() != "Planned") throw new InvalidAgentSourceException();
            var plan = pending.GetProperty("plan"); pendingDue = plan.GetProperty("dueAtUtc").GetDateTime(); pendingChannel = plan.GetProperty("channel").GetString();
            if (pendingChannel is null || !Channels.Contains(pendingChannel, StringComparer.Ordinal)) throw new InvalidAgentSourceException();
        }
        else if (pending.ValueKind != JsonValueKind.Null) throw new InvalidAgentSourceException();
        var now = clock.GetUtcNow().UtcDateTime;
        var text = FollowUpSuggestionPrompt.Freeze(root.GetProperty("jobTitle").GetString()!, root.GetProperty("companyName").GetString()!, status,
            submittedAt, lastUpdate, pendingDue, pendingChannel, now);
        return new(owner, candidacyId, text, AgentRun.Hash(text));
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!names.Add(property.Name)) throw new InvalidAgentSourceException(); RejectDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
    }
}
