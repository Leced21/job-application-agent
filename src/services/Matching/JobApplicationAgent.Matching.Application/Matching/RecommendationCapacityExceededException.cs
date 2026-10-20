namespace JobApplicationAgent.Matching.Application.Matching;

public sealed class RecommendationCapacityExceededException()
    : Exception("The catalogue exceeds the synchronous recommendation processing limits.");
