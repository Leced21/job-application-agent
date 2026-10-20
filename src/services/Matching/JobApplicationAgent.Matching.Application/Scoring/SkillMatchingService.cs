
using System.Text.RegularExpressions;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class SkillMatchingService : ISkillMatchingService
{
    private static readonly IReadOnlyDictionary<string, string[]> SkillAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Python"] = ["python"],
            ["SQL"] = ["sql"],
            ["Apache Spark"] = ["apache spark", "spark", "pyspark"],
            ["Databricks"] = ["databricks"],
            ["Azure"] = ["azure", "microsoft azure"],
            ["AWS"] = ["aws", "amazon web services"],
            ["Docker"] = ["docker"],
            ["Kubernetes"] = ["kubernetes", "k8s"],
            ["Airflow"] = ["airflow", "apache airflow"],
            ["Kafka"] = ["kafka", "apache kafka"]
        };

    public SkillMatchingResult Evaluate(
        IReadOnlyCollection<string> candidateSkills,
        string? jobDescription)
    {
        ArgumentNullException.ThrowIfNull(candidateSkills);

        if (candidateSkills.Count == 0 ||
            string.IsNullOrWhiteSpace(jobDescription))
        {
            return NotEvaluated();
        }

        var requiredSkills = SkillAliases
            .Where(skill => skill.Value.Any(
                alias => ContainsTerm(jobDescription, alias)))
            .Select(skill => skill.Key)
            .OrderBy(skill => skill)
            .ToArray();

        if (requiredSkills.Length == 0)
        {
            return NotEvaluated();
        }

        var normalizedCandidateSkills = candidateSkills
            .Select(NormalizeSkill)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matchedSkills = requiredSkills
            .Where(normalizedCandidateSkills.Contains)
            .ToArray();

        var missingSkills = requiredSkills
            .Except(matchedSkills, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var score = Math.Round(
            (decimal)matchedSkills.Length / requiredSkills.Length * 100m,
            2,
            MidpointRounding.AwayFromZero);

        return new SkillMatchingResult(
            score,
            requiredSkills,
            matchedSkills,
            missingSkills);
    }

    private static SkillMatchingResult NotEvaluated() =>
        new(
            null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static string NormalizeSkill(string skill)
    {
        foreach (var entry in SkillAliases)
        {
            if (entry.Value.Any(
                alias => string.Equals(
                    alias,
                    skill.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
            {
                return entry.Key;
            }
        }

        return skill.Trim();
    }

    private static bool ContainsTerm(string text, string term)
    {
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])";

        return Regex.IsMatch(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }
}
