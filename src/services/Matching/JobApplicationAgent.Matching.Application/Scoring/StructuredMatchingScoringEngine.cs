
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class StructuredMatchingScoringEngine : IMatchingScoringEngine
{
    public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(
        ProfileMatchingData profile,
        JobMatchingData job,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyCollection<MatchingCriterion> criteria =
        [
            EvaluateJobTitle(profile, job),
            EvaluateSkills(profile, job),
            EvaluateLocation(profile, job)
        ];

        return Task.FromResult(criteria);
    }

    private static MatchingCriterion EvaluateJobTitle(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        const decimal weight = 0.30m;

        if (string.IsNullOrWhiteSpace(profile.JobTitle) ||
            string.IsNullOrWhiteSpace(job.Title))
        {
            return MatchingCriterion.NotEvaluated(
                "JobTitle",
                weight,
                "Candidate or job title is missing.");
        }

        var candidateTitle = Normalize(profile.JobTitle);
        var jobTitle = Normalize(job.Title);

        var score = candidateTitle == jobTitle ? 100m : 0m;

        return new MatchingCriterion(
            "JobTitle",
            score,
            weight,
            score == 100m
                ? "Job titles match exactly."
                : "Job titles do not match exactly.");
    }

    private static MatchingCriterion EvaluateSkills(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        const decimal weight = 0.50m;

        if (profile.Skills.Count == 0 ||
            string.IsNullOrWhiteSpace(job.Description))
        {
            return MatchingCriterion.NotEvaluated(
                "Skills",
                weight,
                "Candidate skills or job description are missing.");
        }

        // A job description is unstructured text.
        // We cannot reliably infer all required skills yet.
        // A semantic/requirement extraction component will handle this.
        return MatchingCriterion.NotEvaluated(
            "Skills",
            weight,
            "Job skill requirements have not been extracted yet.");
    }

    private static MatchingCriterion EvaluateLocation(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        const decimal weight = 0.20m;

        var locations = profile.Preferences?.PreferredLocations;

        if (locations is null ||
            locations.Length == 0 ||
            string.IsNullOrWhiteSpace(job.Location))
        {
            return MatchingCriterion.NotEvaluated(
                "Location",
                weight,
                "Location preferences or job location are missing.");
        }

        var normalizedJobLocation = Normalize(job.Location);

        var matches = locations.Any(
            location => Normalize(location) == normalizedJobLocation);

        return new MatchingCriterion(
            "Location",
            matches ? 100m : 0m,
            weight,
            matches
                ? "Job location matches candidate preferences."
                : "Job location does not match candidate preferences.");
    }

    private static string Normalize(string value) =>
        value.Trim().ToUpperInvariant();
}
