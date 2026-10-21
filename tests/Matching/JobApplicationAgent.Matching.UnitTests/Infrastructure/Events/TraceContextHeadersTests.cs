using System.Diagnostics;
using JobApplicationAgent.Observability;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Infrastructure.Events;

public sealed class TraceContextHeadersTests
{
    [Fact]
    public void InjectAndExtract_ShouldPreserveW3CTraceContext()
    {
        var producer = new Activity("publish").SetIdFormat(ActivityIdFormat.W3C).Start();
        try
        {
            Assert.NotNull(producer);
            producer!.TraceStateString = "vendor=value";
            var headers = new Dictionary<string, object?>();

            TraceContextHeaders.Inject(headers);
            var extracted = TraceContextHeaders.Extract(headers);

            Assert.Equal(producer.TraceId, extracted.TraceId);
            Assert.Equal(producer.SpanId, extracted.SpanId);
            Assert.Equal("vendor=value", extracted.TraceState?.ToString() ?? string.Empty);
            Assert.True(extracted.IsRemote);
        }
        finally
        {
            producer?.Stop();
        }
    }

    [Fact]
    public void Extract_ShouldReturnDefaultContext_WhenTraceParentIsInvalid()
    {
        var context = TraceContextHeaders.Extract(new Dictionary<string, object?>
        {
            ["traceparent"] = "not-a-trace-context"
        });

        Assert.Equal(default, context);
    }
}
