using System.Text;
using JobApplicationAgent.Matching.Application.Abstractions;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class JobTitleSimilarityService : IJobTitleSimilarityService
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data engineering"] = "data engineer",
            ["ingenieur data"] = "data engineer",
            ["ingenieur donnees"] = "data engineer",
            ["data analysis"] = "data analyst",
            ["analyste de donnees"] = "data analyst",
            ["analyste donnees"] = "data analyst",
            ["analytics engineering"] = "analytics engineer"
        };

    private static readonly IReadOnlyDictionary<string, decimal> RelatedTitles =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [Pair("data engineer", "data analyst")] = 65m,
            [Pair("data engineer", "analytics engineer")] = 85m,
            [Pair("data analyst", "analytics engineer")] = 75m,
            [Pair("data engineer", "software engineer")] = 45m
        };

    public decimal Calculate(string candidateTitle, string jobTitle)
    {
        var candidate = Normalize(candidateTitle);
        var job = Normalize(jobTitle);

        if (string.IsNullOrWhiteSpace(candidate) ||
            string.IsNullOrWhiteSpace(job))
        {
            return 0m;
        }

        candidate = ResolveAlias(candidate);
        job = ResolveAlias(job);

        if (candidate == job)
        {
            return 100m;
        }

        return RelatedTitles.TryGetValue(
            Pair(candidate, job),
            out var score)
                ? score
                : 0m;
    }

    private static string ResolveAlias(string title)
    {
        return Aliases.TryGetValue(title, out var canonical)
            ? canonical
            : title;
    }

    private static string Pair(string first, string second)
    {
        return string.CompareOrdinal(first, second) <= 0
            ? $"{first}|{second}"
            : $"{second}|{first}";
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var character in decomposed)
        {
            var category =
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(
                    character);

            if (category ==
                System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.IsLetterOrDigit(character)
                    ? character
                    : ' ');
        }

        return string.Join(
            " ",
            builder.ToString().Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));
    }
}
