using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;

namespace JobApplicationAgent.Matching.Application.Scoring;

public sealed class StructuredMatchingScoringEngine : IMatchingScoringEngine
{
    private readonly IJobTitleSimilarityService _jobTitleSimilarityService;
    private readonly ISkillMatchingService _skillMatchingService;
    private readonly ILocationMatchingService _locationMatchingService;
    private readonly ScoringRules _rules;
    public string Version { get; }

    public StructuredMatchingScoringEngine(IJobTitleSimilarityService jobTitleSimilarityService, ISkillMatchingService skillMatchingService,
        ILocationMatchingService locationMatchingService, ScoringRules? rules = null)
    {
        _jobTitleSimilarityService = jobTitleSimilarityService;
        _skillMatchingService = skillMatchingService;
        _locationMatchingService = locationMatchingService;
        _rules = rules ?? new ScoringRules();
        _rules.Validate();
        Version = _rules.Identifier;

    }
    public Task<IReadOnlyCollection<MatchingCriterion>> EvaluateAsync(ProfileMatchingData profile, JobMatchingData job, CancellationToken cancellationToken = default)
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


    private MatchingCriterion EvaluateJobTitle(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        var weight = _rules.JobTitleWeight;
        if (weight == 0m) return Disabled("JobTitle");

        if (string.IsNullOrWhiteSpace(profile.JobTitle) ||
            string.IsNullOrWhiteSpace(job.Title))
        {
            return MatchingCriterion.NotEvaluated(
                "JobTitle",
                weight,
                "Candidate or job title is missing.");
        }

        var score = _jobTitleSimilarityService.Calculate(
            profile.JobTitle,
            job.Title);

        var explanation = score switch
        {
            100m => "Job titles match.",
            > 0m => "Job titles are related but not identical.",
            _ => "No known similarity between job titles."
        };

        return new MatchingCriterion(
            "JobTitle",
            score,
            weight,
            explanation);
    }



    private MatchingCriterion EvaluateSkills(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        var weight = _rules.SkillsWeight;
        if (weight == 0m) return Disabled("Skills");

        var candidateSkills = profile.Skills
            .Select(skill => skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        var result = _skillMatchingService.Evaluate(
            candidateSkills,
            job.Description);

        if (!result.Score.HasValue)
        {
            return MatchingCriterion.NotEvaluated(
                "Skills",
                weight,
                "No evaluable skill requirements were detected, or candidate skills are missing.");
        }

        var matched = string.Join(", ", result.MatchedSkills);
        var missing = string.Join(", ", result.MissingSkills);

        return new MatchingCriterion(
            "Skills",
            result.Score.Value,
            weight,
            $"Matched skills: [{matched}]. Missing skills: [{missing}].");
    }



    private MatchingCriterion EvaluateLocation(
        ProfileMatchingData profile,
        JobMatchingData job)
    {
        var weight = _rules.LocationWeight;
        if (weight == 0m) return Disabled("Location");

        var result = _locationMatchingService.Evaluate(
            profile.Preferences,
            job.Location,
            job.WorkMode);

        if (!result.Score.HasValue)
        {
            return MatchingCriterion.NotEvaluated(
                "Location",
                weight,
                result.Explanation);
        }

        return new MatchingCriterion(
            "Location",
            result.Score.Value,
            weight,
            result.Explanation);
    }


    private static MatchingCriterion Disabled(string name) =>
        MatchingCriterion.NotApplicable(name, 0m, "Criterion disabled by scoring configuration.");
}
