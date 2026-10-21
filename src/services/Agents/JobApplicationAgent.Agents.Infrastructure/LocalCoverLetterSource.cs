using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed record CoverLetterOptions(bool Enabled, string ProfileBaseUrl, string JobBaseUrl)
{
    public static CoverLetterOptions From(IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Agents:CoverLetter:Enabled");
        var profileOrigin = config["Agents:CoverLetter:ProfileBaseUrl"] ?? "http://localhost:5001";
        var jobOrigin = config["Agents:CoverLetter:JobBaseUrl"] ?? "http://localhost:5002";
        _ = OllamaOptions.Origin(profileOrigin); _ = LocalJobSource.ValidateOrigin(jobOrigin);
        return new(enabled, profileOrigin, jobOrigin);
    }
}

public sealed class LocalCoverLetterSource(HttpClient profileHttp, HttpClient jobHttp, IAgentRunRepository runs, CoverLetterOptions options)
{
    public Task<FrozenAgentSource?> ReadAsync(Guid owner, Guid jobId, CancellationToken ct)
    {
        if (!options.Enabled || owner == Guid.Empty || jobId == Guid.Empty) return Task.FromResult<FrozenAgentSource?>(null);
        var sourceOptions = new ResumeAdaptationOptions(true, options.ProfileBaseUrl, options.JobBaseUrl);
        return new LocalResumeSource(profileHttp, jobHttp, runs, sourceOptions).ReadAsync(owner, jobId, ct);
    }
}
