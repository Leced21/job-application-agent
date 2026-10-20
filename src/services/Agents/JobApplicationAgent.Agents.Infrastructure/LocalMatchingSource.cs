using System.Net;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record MatchingEnrichmentOptions(bool Enabled, string BaseUrl)
{
    public static MatchingEnrichmentOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:MatchingEnrichment:Enabled");
        var origin = config["Agents:MatchingEnrichment:BaseUrl"] ?? "http://localhost:5003";
        _ = OllamaOptions.Origin(origin);
        return new(enabled, origin);
    }
}

public sealed class LocalMatchingSource(HttpClient http, MatchingEnrichmentOptions options)
{
    public async Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid resultId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || resultId == Guid.Empty) return null;
        using var response = await http.GetAsync($"api/v1/matching/results/{resultId:D}", HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 32768) throw new InvalidAgentSourceException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream();
        var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > 32768) throw new InvalidAgentSourceException();
            buffer.Write(chunk, 0, read);
        }
        using var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
        var root = doc.RootElement;
        RejectDuplicates(root);
        if (root.GetProperty("id").GetGuid() != resultId || root.GetProperty("candidateProfileId").GetGuid() == Guid.Empty) return null;
        if (root.GetProperty("status").GetString() != "Completed") return null;
        var frozen = MatchingEnrichmentPrompt.Freeze(root);
        return new(owner, resultId, frozen, AgentRun.Hash(frozen));
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
