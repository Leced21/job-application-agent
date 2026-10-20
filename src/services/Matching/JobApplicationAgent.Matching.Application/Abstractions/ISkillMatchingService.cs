using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface ISkillMatchingService
{
    SkillMatchingResult Evaluate(IReadOnlyCollection<string> candidateSkills,string? jobDescription);
}
