using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.Api;

internal static class SubmissionPlatformConfiguration
{
    public static ISubmissionPlatformCatalog From(IConfiguration configuration)
    {
        var section = configuration.GetSection("Applications:Submission");
        var enabledValue = section["Enabled"];
        var enabled = false;
        if (enabledValue is not null && !bool.TryParse(enabledValue, out enabled))
            throw new InvalidOperationException("Applications:Submission:Enabled must be true or false.");

        var platforms = new List<SubmissionPlatformCapability>();
        foreach (var entry in section.GetSection("Platforms").GetChildren())
        {
            string Required(string key) => entry[key] ?? throw new InvalidOperationException($"{entry.Path}:{key} is required.");
            bool RequiredBoolean(string key) => bool.TryParse(Required(key), out var value)
                ? value
                : throw new InvalidOperationException($"{entry.Path}:{key} must be true or false.");

            platforms.Add(new SubmissionPlatformCapability(
                Required("PlatformId"),
                RequiredBoolean("HasOfficialApi"),
                RequiredBoolean("TermsAllowAutomation"),
                RequiredBoolean("RequiresCaptcha"),
                RequiredBoolean("SupportsIdempotencyKey")));
        }

        return new SubmissionPlatformCatalog(enabled, platforms);
    }
}
