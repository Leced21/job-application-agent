using JobApplicationAgent.Matching.Application.Scoring;

namespace JobApplicationAgent.Matching.UnitTests.Scoring;

public sealed class SkillMatchingServiceTests
{
    private readonly SkillMatchingService _service = new();

    [Fact]
    public void Evaluate_ShouldReturn100_WhenAllRequiredSkillsMatch()
    {
        var candidateSkills = new[]
        {
            "Python", "SQL", "PySpark"
        };

        var result = _service.Evaluate(
            candidateSkills,
            "Experience with Python, SQL and Apache Spark.");

        Assert.Equal(100m, result.Score);
        Assert.Equal(3, result.RequiredSkills.Count);
        Assert.Empty(result.MissingSkills);
    }

    [Fact]
    public void Evaluate_ShouldReturnPartialScore_WhenSkillsAreMissing()
    {
        var candidateSkills = new[]
        {
            "Python", "SQL", "Azure"
        };

        var result = _service.Evaluate(
            candidateSkills,
            "Required: Python, SQL, Spark and AWS.");

        Assert.Equal(50m, result.Score);
        Assert.Equal(2, result.MatchedSkills.Count);
        Assert.Equal(2, result.MissingSkills.Count);
    }

    [Fact]
    public void Evaluate_ShouldReturnNull_WhenNoRequiredSkillsAreDetected()
    {
        var result = _service.Evaluate(
            ["Python", "SQL"],
            "Analyse et valorisation des données.");

        Assert.Null(result.Score);
        Assert.Empty(result.RequiredSkills);
    }

    [Fact]
    public void Evaluate_ShouldReturnNull_WhenCandidateHasNoSkills()
    {
        var result = _service.Evaluate(
            Array.Empty<string>(),
            "Required: Python and SQL.");

        Assert.Null(result.Score);
    }

    [Fact]
    public void Evaluate_ShouldNotMatchPartialWords()
    {
        var result = _service.Evaluate(
            ["SQL"],
            "Experience with NoSQL databases.");

        Assert.Null(result.Score);
    }
}
