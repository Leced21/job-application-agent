namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class RecommendationProcessingLimits
{
    public int MaxUniqueJobs { get; init; } = 10_000;
    public int MaxSourcePages { get; init; } = 200;
    public int TimeoutSeconds { get; init; } = 30;

    public void Validate()
    {
        if (MaxUniqueJobs is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(MaxUniqueJobs));
        if (MaxSourcePages is < 1 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(MaxSourcePages));
        if (TimeoutSeconds is < 1 or > 300)
            throw new ArgumentOutOfRangeException(nameof(TimeoutSeconds));
    }
}
