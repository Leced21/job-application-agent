using JobApplicationAgent.Matching.Application.Models;

namespace JobApplicationAgent.Matching.Application.Abstractions;

public interface ILocationMatchingService
{
    LocationMatchingResult Evaluate( MatchingPreferencesData? preferences, string? jobLocation, string? jobWorkMode);
}
