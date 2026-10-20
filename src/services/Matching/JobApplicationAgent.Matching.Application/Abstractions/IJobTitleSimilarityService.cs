namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface IJobTitleSimilarityService
{
    decimal Calculate(string candidateTitle, string jobTitle);
}