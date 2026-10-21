using System.Diagnostics;
using System.Text;

namespace JobApplicationAgent.Observability;

public static class TraceContextHeaders
{
    public static void Inject(IDictionary<string, object?> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var activity = Activity.Current;
        if (activity?.Id is not { Length: > 0 } traceParent) return;

        headers["traceparent"] = Encoding.UTF8.GetBytes(traceParent);
        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
            headers["tracestate"] = Encoding.UTF8.GetBytes(activity.TraceStateString);
    }

    public static ActivityContext Extract(IDictionary<string, object?>? headers)
    {
        if (headers is null || !TryRead(headers, "traceparent", out var traceParent))
            return default;
        TryRead(headers, "tracestate", out var traceState);
        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context)
            ? context
            : default;
    }

    private static bool TryRead(IDictionary<string, object?> headers, string key, out string value)
    {
        value = string.Empty;
        if (!headers.TryGetValue(key, out var raw)) return false;
        value = raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(value);
    }
}
