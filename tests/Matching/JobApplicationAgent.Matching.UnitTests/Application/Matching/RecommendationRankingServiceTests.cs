
using JobApplicationAgent.Matching.Application.Matching;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class RecommendationRankingServiceTests
{
    private readonly RecommendationRankingService _service = new();

    [Theory]
    [InlineData(100, 50, 50)]
    [InlineData(90, 100, 90)]
    [InlineData(39, 50, 19.5)]
    [InlineData(0, 50, 0)]
    [InlineData(80, 75, 60)]
    [InlineData(80, 100, 80)]
    [InlineData(100, 0, 0)]
    public void Calculate_ShouldApplyCoverage(
        decimal overallScore,
        decimal coverage,
        decimal expected)
    {
        var result = _service.Calculate(
            overallScore,
            coverage);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Calculate_ShouldReturnNull_WhenScoreIsMissing()
    {
        var result = _service.Calculate(null, 0m);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Calculate_ShouldRejectInvalidOverallScore(
        decimal overallScore)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Calculate(overallScore, 100m));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Calculate_ShouldRejectInvalidCoverage(
        decimal coverage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Calculate(80m, coverage));
    }

    [Fact]
    public void Calculate_ShouldRoundToTwoDecimals()
    {
        var result = _service.Calculate(66.67m, 33.33m);

        Assert.Equal(22.22m, result);
    }
}
