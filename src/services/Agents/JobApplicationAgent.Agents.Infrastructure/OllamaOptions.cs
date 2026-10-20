using System.Text.RegularExpressions;
using JobApplicationAgent.Agents.Application;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record OllamaOptions
{
    public string Provider { get; init; } = "Disabled";
    public string BaseUrl { get; init; } = "http://127.0.0.1:11434";
    public string ModelName { get; init; } = "";
    public string ModelDigest { get; init; } = "";
    public string TokenizerProfile { get; init; } = "";
    public string TokenizerPath { get; init; } = "";
    public string TokenizerSha256 { get; init; } = "";
    public int MaxInputTokens { get; init; }
    public int MaxOutputTokens { get; init; }
    public int ContextSize { get; init; }
    public int TimeoutSeconds { get; init; }
    public bool AllowJobDescriptions { get; init; }
    public bool LocalServerCloudDisabled { get; init; }
    public bool Enabled => Provider == "Ollama";
    public string ModelId => Enabled ? $"{ModelName}@sha256:{ModelDigest}" : "disabled";
    public AgentPolicy Policy => Enabled ? new(ModelId, MaxInputTokens, MaxOutputTokens, 0, 0, 0, TimeSpan.FromSeconds(TimeoutSeconds))
        : new("disabled", 4000, 1000, 0, 0, 0, TimeSpan.FromSeconds(30));
    public static OllamaOptions From(IConfiguration config)
    {
        var options = config.GetSection("Agents:Model").Get<OllamaOptions>() ?? new(); options.Validate(); return options;
    }
    public static Uri Origin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !(LocalJobSource.IsLocalOrigin(uri, "profile-api") || LocalJobSource.IsLocalOrigin(uri, "matching-api")) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("A loopback origin or the local Compose Profile/Matching origin is required.");
        return uri;
    }
    public static Uri ModelOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !(uri.IsLoopback || (uri.Scheme == "http" && uri.Host.Equals("agents-ollama", StringComparison.Ordinal) && uri.Port == 11434)) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Ollama must use loopback or the private local Compose agents-ollama service.");
        return uri;
    }
    public void Validate()
    {
        if (Provider is not ("Disabled" or "Ollama")) throw new ArgumentException("Unsupported Agents model provider.");
        if (!Enabled) return;
        _ = ModelOrigin(BaseUrl);
        if (!Regex.IsMatch(ModelName, "^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,99}$") || ModelName.LastIndexOf(':') <= ModelName.LastIndexOf('/') ||
            ModelName.Contains("cloud", StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(ModelDigest, "^[a-f0-9]{64}$") || !Regex.IsMatch(TokenizerSha256, "^[a-f0-9]{64}$") ||
            TokenizerProfile is not ("Llama3" or "Qwen2") || !Path.IsPathFullyQualified(TokenizerPath) ||
            ContextSize is < 512 or > 65536 || TimeoutSeconds is < 1 or > 120 ||
            (long)MaxInputTokens + MaxOutputTokens + 1 > ContextSize || !AllowJobDescriptions || !LocalServerCloudDisabled)
            throw new ArgumentException("Configure explicit local model, digest, tokenizer, limits and local data policy.");
        Policy.Validate();
    }
}
