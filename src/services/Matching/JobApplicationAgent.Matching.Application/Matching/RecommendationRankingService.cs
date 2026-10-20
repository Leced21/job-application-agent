
namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class RecommendationRankingService
{
    public decimal? Calculate(
        decimal? overallScore,
        decimal evaluationCoverage)
    {
        if (overallScore is null)
        {
            return null;
        }

        if (overallScore < 0m || overallScore > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overallScore));
        }

        if (evaluationCoverage < 0m ||
            evaluationCoverage > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evaluationCoverage));
        }

        var rankingScore =
            overallScore.Value * evaluationCoverage / 100m;

        return Math.Round(
            rankingScore,
            2,
            MidpointRounding.AwayFromZero);
    }
}
