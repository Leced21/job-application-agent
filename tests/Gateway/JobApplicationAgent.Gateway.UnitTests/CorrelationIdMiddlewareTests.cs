using JobApplicationAgent.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobApplicationAgent.Gateway.UnitTests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ShouldKeepValidCorrelationIdAndReturnIt()
    {
        var context = new DefaultHttpContext();
        var expected = Guid.NewGuid().ToString("D");
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = expected;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        Assert.Equal(expected, context.Items[CorrelationIdMiddleware.HeaderName]);
        Assert.Equal(expected, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReplaceMalformedCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "invalid\r\nid";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(Guid.TryParse(context.Items[CorrelationIdMiddleware.HeaderName]?.ToString(), out _));
    }
}
