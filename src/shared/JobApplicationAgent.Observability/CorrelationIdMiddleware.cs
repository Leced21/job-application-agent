using System.Diagnostics;

namespace JobApplicationAgent.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var value = context.Request.Headers[HeaderName];
        var supplied = value.Count == 1 ? value[0] : null;
        var correlationId = Guid.TryParse(supplied, out var parsed) ? parsed.ToString("D") : Guid.NewGuid().ToString("D");
        context.Items[HeaderName] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            Activity.Current?.SetTag("correlation.id", correlationId);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                HttpRequestMetrics.Record(context, stopwatch.Elapsed.TotalSeconds);
                logger.LogInformation(
                    "HTTP request completed with status code {StatusCode} in {ElapsedMilliseconds} ms. CorrelationId: {CorrelationId}",
                    context.Response.StatusCode, stopwatch.ElapsedMilliseconds, correlationId);
            }
        }
    }
}
