using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace JobApplicationAgent.Observability;

public static class EventTransportMetrics
{
    public static readonly ActivitySource ActivitySource = new("JobApplicationAgent.EventTransport", "1.0.0");
    private static readonly Meter Meter = new("JobApplicationAgent.EventTransport");
    private static readonly Counter<long> OutboxDispatches =
        Meter.CreateCounter<long>("jobagent.messaging.outbox.dispatches");
    private static readonly Histogram<double> OutboxDispatchDuration =
        Meter.CreateHistogram<double>("jobagent.messaging.outbox.dispatch.duration", "s");
    private static readonly Counter<long> ConsumerDeliveries =
        Meter.CreateCounter<long>("jobagent.messaging.consumer.deliveries");
    private static readonly Histogram<double> ConsumerDuration =
        Meter.CreateHistogram<double>("jobagent.messaging.consumer.duration", "s");

    public static long StartMeasurement() => Stopwatch.GetTimestamp();

    public static void RecordOutboxDispatch(long startedAt, string service, bool published)
    {
        var outcome = published ? "published" : "failed";
        OutboxDispatches.Add(1,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("outcome", outcome));
        OutboxDispatchDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            new KeyValuePair<string, object?>("service", service),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RecordConsumerDelivery(long startedAt, string consumer, string outcome)
    {
        ConsumerDeliveries.Add(1,
            new KeyValuePair<string, object?>("consumer", consumer),
            new KeyValuePair<string, object?>("outcome", outcome));
        ConsumerDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
            new KeyValuePair<string, object?>("consumer", consumer),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}
