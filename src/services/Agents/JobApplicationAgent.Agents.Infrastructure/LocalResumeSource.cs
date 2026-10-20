using System.Net;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record ResumeAdaptationOptions(bool Enabled, string ProfileBaseUrl, string JobBaseUrl)
{
    public static ResumeAdaptationOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:ResumeAdaptation:Enabled");
        var profileOrigin = config["Agents:ResumeAdaptation:ProfileBaseUrl"] ?? "http://localhost:5001";
        var jobOrigin = config["Agents:ResumeAdaptation:JobBaseUrl"] ?? "http://localhost:5002";
        _ = OllamaOptions.Origin(profileOrigin); _ = LocalJobSource.ValidateOrigin(jobOrigin);
        return new(enabled, profileOrigin, jobOrigin);
    }
}

public sealed class LocalResumeSource(HttpClient profileHttp, HttpClient jobHttp, IAgentRunRepository runs,
    ResumeAdaptationOptions options, bool allowEmptyFacts = false)
{
    public async Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid jobId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || jobId == Guid.Empty) return null;
        var analysis = await runs.GetLatestApprovedAsync(owner, AgentKind.JobAnalysis, jobId, ct);
        if (analysis is null || analysis.State != AgentRunState.Approved || analysis.Proposal is null ||
            analysis.Command.ResourceId != jobId || analysis.Proposal.Source.ResourceId != jobId ||
            analysis.Proposal.Source.Sha256 != AgentRun.Hash(analysis.Proposal.Source.Text)) return null;

        var job = await ReadJsonAsync(jobHttp, $"api/v1/jobs/{jobId:D}", 131072, ct);
        if (job is null || job.Value.GetProperty("id").GetGuid() != jobId ||
            job.Value.GetProperty("status").GetString() is not ("Discovered" or "Saved")) return null;
        var description = job.Value.GetProperty("description").GetString();
        if (string.IsNullOrWhiteSpace(description) || description != analysis.Proposal.Source.Text) return null;

        var profile = await ReadJsonAsync(profileHttp, "api/v1/profile/matching-data", 131072, ct);
        if (profile is null || profile.Value.GetProperty("candidateProfileId").GetGuid() == Guid.Empty) return null;
        var profileId = profile.Value.GetProperty("candidateProfileId").GetGuid();
        var text = ResumeAdaptationPrompt.Freeze(profileId, profile.Value.GetRawText(), analysis.Proposal.OutputJson, description, allowEmptyFacts);
        return new(owner, jobId, text, AgentRun.Hash(text));
    }

    private static async Task<JsonElement?> ReadJsonAsync(HttpClient http, string path, int maxBytes, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidAgentSourceException();
        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        { if (buffer.Length + read > maxBytes) throw new InvalidAgentSourceException(); buffer.Write(chunk, 0, read); }
        using var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
        RejectDuplicates(document.RootElement);
        return document.RootElement.Clone();
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidAgentSourceException();
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item);
        }
    }
}
