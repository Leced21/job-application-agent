
using System.Globalization;
using System.Text;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class LocationMatchingService : ILocationMatchingService
{
    public LocationMatchingResult Evaluate(
        MatchingPreferencesData? preferences,
        string? jobLocation,
        string? jobWorkMode)
    {
        if (preferences is null)
        {
            return NotEvaluated(
                "Candidate preferences are missing.");
        }

        var preferredLocations = preferences.PreferredLocations
            ?? Array.Empty<string>();

        var preferredWorkModes = preferences.WorkModes
            ?? Array.Empty<string>();

        var normalizedWorkMode = Normalize(jobWorkMode);

        var hasWorkModePreferences = preferredWorkModes
            .Any(mode => !string.IsNullOrWhiteSpace(mode));

        var workModeMatches = preferredWorkModes
            .Any(mode => Normalize(mode) == normalizedWorkMode);

        if (hasWorkModePreferences &&
            !string.IsNullOrWhiteSpace(normalizedWorkMode) &&
            !workModeMatches)
        {
            return new LocationMatchingResult(
                0m,
                "Job work mode does not match candidate preferences.");
        }

        if (normalizedWorkMode == "REMOTE" && workModeMatches)
        {
            return new LocationMatchingResult(
                100m,
                "Remote job matches candidate work mode preferences.");
        }

        if (string.IsNullOrWhiteSpace(jobLocation))
        {
            return NotEvaluated(
                "Job location is missing.");
        }

        var hasLocationPreferences = preferredLocations
            .Any(location => !string.IsNullOrWhiteSpace(location));

        if (!hasLocationPreferences)
        {
            return NotEvaluated(
                "Candidate location preferences are missing.");
        }

        var normalizedJobLocation = Normalize(jobLocation);

        var locationMatches = preferredLocations
            .Any(location => Normalize(location) == normalizedJobLocation);

        if (!hasWorkModePreferences)
        {
            return NotEvaluated(
                "Candidate work mode preferences are missing.");
        }

        return locationMatches
            ? new LocationMatchingResult(
                100m,
                "Job location and work mode match candidate preferences.")
            : new LocationMatchingResult(
                0m,
                "Job location does not match candidate preferences.");
    }

    private static LocationMatchingResult NotEvaluated(
        string explanation)
    {
        return new LocationMatchingResult(
            null,
            explanation);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character)
                != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }
}
