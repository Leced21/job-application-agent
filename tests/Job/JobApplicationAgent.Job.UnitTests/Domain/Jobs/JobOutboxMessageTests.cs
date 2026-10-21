using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.UnitTests.Domain.Jobs;

public sealed class JobOutboxMessageTests
{
    [Fact]
    public void Create_ShouldNormalizeCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var message = JobOutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid(), "job.published.v1", 1,
            DateTime.UtcNow, "{}", correlationId.ToString("N"));

        Assert.Equal(correlationId.ToString("D"), message.CorrelationId);
    }

    [Fact]
    public void RecordFailedAttempt_ThenMarkDispatched_ShouldKeepAttemptAndClearRetryState()
    {
        var createdAt = DateTime.SpecifyKind(new DateTime(2026, 10, 16, 10, 0, 0), DateTimeKind.Utc);
        var failedAt = createdAt.AddMinutes(1);
        var retryAt = failedAt.AddSeconds(2);
        var message = JobOutboxMessage.Create(Guid.NewGuid(), Guid.NewGuid(), "job.published.v1", 1,
            createdAt, "{}");

        message.RecordFailedAttempt(failedAt, retryAt, "BrokerUnavailable");

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(retryAt, message.NextAttemptAtUtc);
        Assert.Equal("BrokerUnavailable", message.LastFailureCode);

        message.MarkDispatched(retryAt);

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal(retryAt, message.DispatchedAtUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.LastFailureCode);
    }
}
