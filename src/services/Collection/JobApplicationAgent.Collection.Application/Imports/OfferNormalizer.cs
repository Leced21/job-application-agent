using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobApplicationAgent.Collection.Domain.Entities;

namespace JobApplicationAgent.Collection.Application.Imports;

public sealed class OfferNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> WorkModes = new Dictionary<string, string>
    {
        ["UNKNOWN"] = "Unknown", ["INCONNU"] = "Unknown", ["ONSITE"] = "OnSite", ["ON SITE"] = "OnSite",
        ["PRESENTIEL"] = "OnSite", ["HYBRID"] = "Hybrid", ["HYBRIDE"] = "Hybrid", ["REMOTE"] = "Remote",
        ["TELETRAVAIL"] = "Remote", ["A DISTANCE"] = "Remote"
    };
    private static readonly IReadOnlyDictionary<string, string> Contracts = new Dictionary<string, string>
    {
        ["UNKNOWN"] = "Unknown", ["INCONNU"] = "Unknown", ["PERMANENT"] = "Permanent", ["CDI"] = "Permanent",
        ["FIXEDTERM"] = "FixedTerm", ["FIXED TERM"] = "FixedTerm", ["CDD"] = "FixedTerm", ["FREELANCE"] = "Freelance",
        ["INDEPENDANT"] = "Freelance", ["INTERNSHIP"] = "Internship", ["STAGE"] = "Internship",
        ["APPRENTICESHIP"] = "Apprenticeship", ["ALTERNANCE"] = "Apprenticeship", ["TEMPORARY"] = "Temporary", ["INTERIM"] = "Temporary"
    };
    private static readonly IReadOnlyDictionary<string, string> SkillAliases = new Dictionary<string, string>
    {
        ["PYTHON"] = "Python", ["SQL"] = "SQL", ["SPARK"] = "Apache Spark", ["APACHE SPARK"] = "Apache Spark",
        ["PYSPARK"] = "Apache Spark", ["K8S"] = "Kubernetes", ["KUBERNETES"] = "Kubernetes",
        ["AWS"] = "AWS", ["AMAZON WEB SERVICES"] = "AWS", ["AZURE"] = "Azure", ["MICROSOFT AZURE"] = "Azure"
    };

    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null :
        Regex.Replace(value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static string Key(string value) => new string((Text(value) ?? "").Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
        .ToArray()).Normalize(NormalizationForm.FormC).ToUpperInvariant();
    private static string ModeKey(string value) => Key(value.Replace('-', ' ').Replace('_', ' '));
    public static bool IsWorkMode(string? value) => Text(value) is null || WorkModes.ContainsKey(ModeKey(value!));
    public static bool IsContract(string? value) => Text(value) is null || Contracts.ContainsKey(ModeKey(value!));

    public static bool IsSupportedUrl(string? value)
    {
        if (Text(value) is null) return true;
        if (!Uri.TryCreate(value!.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host)) return false;
        return CanonicalUrl(uri).Length <= 2000;
    }

    private static string CanonicalUrl(Uri uri, bool preserveTracking = false)
    {
        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part =>
            {
                if (preserveTracking) return true;
                var name = Uri.UnescapeDataString(part.Split('=', 2)[0]);
                return !name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) &&
                    !new[] { "fbclid", "gclid", "msclkid" }.Contains(name, StringComparer.OrdinalIgnoreCase);
            });
        return new UriBuilder(uri) { Fragment = "", Query = string.Join("&", query) }.Uri.AbsoluteUri;
    }

    public CollectedOffer Normalize(ImportOfferCommand command, DateTime collectedAtUtc)
    {
        var skills = (command.Skills ?? []).Select(skill => Text(skill)!)
            .Select(skill => SkillAliases.GetValueOrDefault(Key(skill), skill))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var source = Text(command.SourceName) ?? "Manual";
        var preserveTracking = source.Equals("Adzuna", StringComparison.OrdinalIgnoreCase);
        var url = Text(command.SourceUrl) is null ? null : CanonicalUrl(new Uri(command.SourceUrl!.Trim()), preserveTracking);
        var content = new OfferContent(Text(command.Title)!, Text(command.CompanyName)!, Text(command.Location),
            Text(command.WorkMode) is null ? "Unknown" : WorkModes[ModeKey(command.WorkMode!)],
            Text(command.ContractType) is null ? "Unknown" : Contracts[ModeKey(command.ContractType!)],
            command.SalaryMin, command.SalaryMax, Text(command.SalaryCurrency)?.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.ReplaceLineEndings("\n").Trim(),
            skills, source, url, command.PublishedAtUtc, command.ExpiresAtUtc);
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            Title = Key(content.Title), Company = Key(content.CompanyName),
            Location = content.Location is null ? null : Key(content.Location), content.WorkMode, content.ContractType,
            SalaryMin = content.SalaryMin?.ToString("G29", CultureInfo.InvariantCulture),
            SalaryMax = content.SalaryMax?.ToString("G29", CultureInfo.InvariantCulture), content.SalaryCurrency, content.Description,
            Skills = skills.Select(Key).ToArray(), content.PublishedAtUtc, content.ExpiresAtUtc
        }));
        var identity = url is null ? Hash($"content-v1|{Key(source)}|{fingerprint}") : Hash($"url-v1|{url}");
        return CollectedOffer.Create(content, command.Title, identity, fingerprint, collectedAtUtc);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
