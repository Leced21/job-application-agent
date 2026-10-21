using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobApplicationAgent.Matching.Application.Matching;

public static class MatchingCalculationMetrics
{
    public static readonly ActivitySource ActivitySource = new("JobApplicationAgent.Matching", "1.0.0");
    private static readonly Meter Meter = new("JobApplicationAgent.Matching", "1.0.0");
    private static readonly Counter<long> Calculations = Meter.CreateCounter<long>("jobagent.matching.calculations", "{calculation}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("jobagent.matching.calculation.duration", "s");

    public static void Record(double elapsedSeconds, string outcome)
    {
        var tags = new TagList { { "matching.outcome", outcome } };
        Calculations.Add(1, tags);
        Duration.Record(elapsedSeconds, tags);
    }
}
