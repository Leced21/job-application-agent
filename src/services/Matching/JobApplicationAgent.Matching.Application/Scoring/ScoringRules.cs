using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class ScoringRules
{
    // Increment for any change to scoring logic, normalization or synonym dictionaries.
    public const string AlgorithmVersion = "deterministic-v2";
    public string RulesVersion { get; init; } = AlgorithmVersion;
    public decimal JobTitleWeight { get; init; } = .30m;
    public decimal SkillsWeight { get; init; } = .50m;
    public decimal LocationWeight { get; init; } = .20m;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RulesVersion) || RulesVersion.Length > 48 ||
            RulesVersion.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("RulesVersion must contain 1 to 48 ASCII letters, digits, dots, hyphens or underscores.", nameof(RulesVersion));
        foreach (var weight in new[] { JobTitleWeight, SkillsWeight, LocationWeight })
        {
            if (weight is < 0m or > 1m || decimal.Round(weight, 4) != weight)
                throw new ArgumentOutOfRangeException(nameof(weight), "Weights must be between zero and one with at most four decimal places.");
        }
        if (JobTitleWeight + SkillsWeight + LocationWeight != 1m)
            throw new ArgumentException("Scoring weights must sum to one.");
    }

    public string Identifier
    {
        get
        {
            Validate();
            var definition = string.Create(CultureInfo.InvariantCulture,
                $"Algorithm={AlgorithmVersion}|Configuration={RulesVersion}|JobTitle={JobTitleWeight:G29}|Skills={SkillsWeight:G29}|Location={LocationWeight:G29}");
            return $"{AlgorithmVersion}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition)))}";
        }
    }
}
