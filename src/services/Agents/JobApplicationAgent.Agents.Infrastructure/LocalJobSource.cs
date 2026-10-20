using System.Net;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed class LocalJobSource(HttpClient http)
{
    public static Uri ValidateOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !IsLocalOrigin(uri, "job-api") || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("A loopback Job origin or the local Compose job-api origin is required.");
        return uri;
    }

    internal static bool IsLocalOrigin(Uri uri, string composeService) => uri.IsLoopback ||
        (uri.Scheme == "http" && uri.Host.Equals(composeService, StringComparison.Ordinal) && uri.Port == 8080);
    public async Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid resourceId, CancellationToken ct)
    {
        if (owner == Guid.Empty || resourceId == Guid.Empty) return null;
        using var response = await http.GetAsync($"api/v1/jobs/{resourceId:D}", HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 131072) throw new InvalidOperationException("Job response too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > 131072) throw new InvalidOperationException("Job response too large.");
            buffer.Write(chunk, 0, read);
        }
        using var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        var root = json.RootElement;
        if (root.GetProperty("id").GetGuid() != resourceId) return null;
        var status = root.GetProperty("status").GetString();
        if (status is not ("Discovered" or "Saved")) return null;
        var text = root.GetProperty("description").GetString();
        if (string.IsNullOrWhiteSpace(text) || text.Length > 10000) return null;
        return new(owner, resourceId, text, AgentRun.Hash(text));
    }
}

// No simulated provider is registered in production. Counting fails before any quota is reserved.
public sealed class DisabledAgentModel : IAgentModel
{
    public Task<PreparedModelInput> PrepareAsync(string modelId, ModelPrompt prompt, CancellationToken ct) => throw new InvalidOperationException("Model provider not configured.");
    public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken ct) => throw new InvalidOperationException("Model provider not configured.");
}
