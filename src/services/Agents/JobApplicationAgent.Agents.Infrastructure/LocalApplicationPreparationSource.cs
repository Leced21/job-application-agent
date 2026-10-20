using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record ApplicationPreparationOptions(bool Enabled, string ProfileBaseUrl, string JobBaseUrl)
{
    public static ApplicationPreparationOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:ApplicationPreparation:Enabled");
        var profileOrigin = config["Agents:ApplicationPreparation:ProfileBaseUrl"] ?? "http://localhost:5001";
        var jobOrigin = config["Agents:ApplicationPreparation:JobBaseUrl"] ?? "http://localhost:5002";
        _ = OllamaOptions.Origin(profileOrigin); _ = LocalJobSource.ValidateOrigin(jobOrigin);
        return new(enabled, profileOrigin, jobOrigin);
    }
}

public sealed class LocalApplicationPreparationSource(HttpClient profileHttp, HttpClient jobHttp, IAgentRunRepository runs,
    ApplicationPreparationOptions options)
{
    public Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid jobId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || jobId == Guid.Empty) return Task.FromResult<FrozenAgentSource?>(null);
        var sourceOptions = new ResumeAdaptationOptions(true, options.ProfileBaseUrl, options.JobBaseUrl);
        var source = new LocalResumeSource(profileHttp, jobHttp, runs, sourceOptions, allowEmptyFacts: true);
        return source.ReadAsync(owner, jobId, ct);
    }
}
