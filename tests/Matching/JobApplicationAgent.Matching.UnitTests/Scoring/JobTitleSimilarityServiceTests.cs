using JobApplicationAgent.Matching.Application.Scoring;

namespace JobApplicationAgent.Matching.UnitTests.Scoring;

public sealed class JobTitleSimilarityServiceTests
{
    private readonly JobTitleSimilarityService _service = new();

    [Theory]
    [InlineData("Data Engineer", "Data Engineer", 100)]
    [InlineData("DATA ENGINEER", "data engineer", 100)]
    [InlineData("  Data   Engineer ", "Data Engineer", 100)]
    [InlineData("Ingénieur Data", "Data Engineer", 100)]
    [InlineData("Data Engineer", "Data Analyst", 65)]
    [InlineData("Data Engineer", "Analytics Engineer", 85)]
    [InlineData("Data Analyst", "Analytics Engineer", 75)]
    [InlineData("Data Engineer", "Software Engineer", 45)]
    [InlineData("Data Engineer", "Accountant", 0)]
    [InlineData("", "Data Engineer", 0)]
    [InlineData("Data Engineer", "", 0)]
    public void Calculate_ShouldReturnExpectedScore(
        string candidateTitle,
        string jobTitle,
        int expectedScore)
    {
        var score = _service.Calculate(
            candidateTitle,
            jobTitle);

        Assert.Equal((decimal)expectedScore, score);
    }

    [Fact]
    public void Calculate_ShouldBeSymmetric()
    {
        var forward = _service.Calculate(
            "Data Engineer",
            "Data Analyst");

        var backward = _service.Calculate(
            "Data Analyst",
            "Data Engineer");

        Assert.Equal(forward, backward);
    }
}
