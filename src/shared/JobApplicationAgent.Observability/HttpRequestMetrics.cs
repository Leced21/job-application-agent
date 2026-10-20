using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace JobApplicationAgent.Observability;

public static class HttpRequestMetrics
{
    private static readonly Meter Meter = new("JobApplicationAgent.Http", "1.0.0");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("jobagent.http.server.requests", "{request}");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("jobagent.http.server.request.duration", "s");

    public static void Record(HttpContext context, double elapsedSeconds)
    {
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
        var tags = new TagList
        {
            { "http.request.method", context.Request.Method },
            { "http.route", route },
            { "http.response.status_code", context.Response.StatusCode }
        };
        Requests.Add(1, tags);
        Duration.Record(elapsedSeconds, tags);
    }
}
