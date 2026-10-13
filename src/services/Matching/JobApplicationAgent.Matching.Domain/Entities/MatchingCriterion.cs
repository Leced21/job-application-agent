using JobApplicationAgent.Matching.Domain.Enums;

namespace JobApplicationAgent.Matching.Domain.Entities;

public sealed class MatchingCriterion
{
    public string Name { get; }
    public decimal? Score { get; }
    public decimal Weight { get; }
    public string Explanation { get; }
    public MatchingCriterionStatus Status { get; }

    public MatchingCriterion(
        string name,
        decimal score,
        decimal weight,
        string explanation)
        : this(
            name,
            score,
            weight,
            explanation,
            MatchingCriterionStatus.Evaluated)
    {
    }

    private MatchingCriterion(
        string name,
        decimal? score,
        decimal weight,
        string explanation,
        MatchingCriterionStatus status)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Criterion name is required.", nameof(name));

        if (weight < 0 || weight > 1)
            throw new ArgumentOutOfRangeException(nameof(weight));

        if (status == MatchingCriterionStatus.Evaluated &&
            (score is null || score < 0 || score > 100))
            throw new ArgumentOutOfRangeException(nameof(score));

        if (status == MatchingCriterionStatus.NotEvaluated &&
            score is not null)
            throw new ArgumentException(
                "A non-evaluated criterion cannot have a score.",
                nameof(score));

        Name = name;
        Score = score;
        Weight = weight;
        Explanation = explanation ?? string.Empty;
        Status = status;
    }

    public static MatchingCriterion NotEvaluated(
        string name,
        decimal weight,
        string explanation)
    {
        return new MatchingCriterion(
            name,
            null,
            weight,
            explanation,
            MatchingCriterionStatus.NotEvaluated);
    }
}
