using System.Net;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record ProfileAnalysisOptions(bool Enabled, string BaseUrl)
{
    public static ProfileAnalysisOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:ProfileAnalysis:Enabled");
        var origin = config["Agents:ProfileAnalysis:BaseUrl"] ?? "http://localhost:5001";
        _ = OllamaOptions.Origin(origin);
        return new(enabled, origin);
    }
}
public sealed class LocalProfileSource(HttpClient http, ProfileAnalysisOptions options)
{
    public async Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid resourceId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || resourceId == Guid.Empty) return null;
        using var response = await http.GetAsync("api/v1/profile/matching-data", HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 131072) throw new InvalidAgentSourceException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > 131072) throw new InvalidAgentSourceException();
            buffer.Write(chunk, 0, read);
        }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
        var root = document.RootElement;
        RejectDuplicates(root);
        if (root.GetProperty("candidateProfileId").GetGuid() != resourceId) return null;
        var present = new Dictionary<string, bool> {
            ["JobTitle"] = HasText(root, "jobTitle", 200), ["Summary"] = HasText(root, "summary", 10000),
            ["Skills"] = HasRecords(root, "skills", "name"), ["Experiences"] = HasRecords(root, "experiences", "jobTitle", "companyName"),
            ["Educations"] = HasRecords(root, "educations", "degree", "institutionName"), ["Languages"] = HasRecords(root, "languages", "name", "proficiencyLevel") };
        var preferences = root.GetProperty("preferences");
        foreach (var (field, property) in new[] { ("DesiredJobTitles", "desiredJobTitles"), ("PreferredLocations", "preferredLocations"),
            ("ContractTypes", "contractTypes"), ("WorkModes", "workModes") })
            present[field] = preferences.ValueKind != JsonValueKind.Null && HasStrings(preferences, property);
        present["AvailableFrom"] = preferences.ValueKind != JsonValueKind.Null && HasDate(preferences, "availableFrom");
        // Discard all raw profile text, employers, dates, identifiers and contact fields before freezing/model preparation.
        var text = ProfileAnalysisPrompt.FreezePresence(present);
        return new(owner, resourceId, text, AgentRun.Hash(text));
    }
    private static bool HasText(JsonElement root, string name, int limit)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return false;
        var text = value.GetString(); if (text?.Length > limit) throw new InvalidAgentSourceException();
        return !string.IsNullOrWhiteSpace(text);
    }
    private static bool HasRecords(JsonElement root, string name, params string[] required)
    {
        var array = root.GetProperty(name);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 100) throw new InvalidAgentSourceException();
        foreach (var item in array.EnumerateArray())
            if (required.Any(field => !HasText(item, field, 500))) throw new InvalidAgentSourceException();
        return array.GetArrayLength() != 0;
    }
    private static bool HasStrings(JsonElement root, string name)
    {
        var array = root.GetProperty(name);
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 100 ||
            array.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) || item.GetString()!.Length > 200))
            throw new InvalidAgentSourceException();
        return array.GetArrayLength() != 0;
    }
    private static bool HasDate(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null) return false;
        if (value.ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out _)) throw new InvalidAgentSourceException();
        return true;
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
public sealed class LocalAgentSources(LocalJobSource jobs, LocalProfileSource profiles, LocalMatchingSource matching, LocalResumeSource resume,
    LocalCoverLetterSource coverLetter, LocalApplicationPreparationSource applicationPreparation, LocalFollowUpSuggestionSource followUp) : IAgentSourceReader
{
    public Task<FrozenAgentSource?> ReadAsync(Guid owner, AgentCommand command, CancellationToken ct) => command.Kind switch {
        AgentKind.JobAnalysis => jobs.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.ProfileAnalysis => profiles.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.MatchingEnrichment => matching.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.ResumeAdaptation => resume.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.CoverLetter => coverLetter.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.ApplicationPreparation => applicationPreparation.ReadAsync(owner, command.ResourceId, ct),
        AgentKind.FollowUpSuggestion => followUp.ReadAsync(owner, command.ResourceId, ct),
        _ => Task.FromResult<FrozenAgentSource?>(null) };
}
