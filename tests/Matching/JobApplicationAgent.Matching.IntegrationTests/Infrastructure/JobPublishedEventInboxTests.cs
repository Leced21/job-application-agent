using System.Text.Json;
using JobApplicationAgent.Matching.Application.Events;
using JobApplicationAgent.Matching.Domain.Entities;
using JobApplicationAgent.Matching.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class JobPublishedEventInboxTests(MatchingPostgresFixture fixture) : IClassFixture<MatchingPostgresFixture>
{
    [Fact]
    public async Task TryRecordProcessedAsync_ShouldAtomicallyPersistResultAndIgnoreDuplicateEvent()
    {
        await using var db = fixture.CreateDbContext();
        var inbox = new JobPublishedEventInbox(db, TimeProvider.System);
        var message = new JobPublishedV1(Guid.NewGuid(), JobPublishedV1.Type, 1, DateTime.UtcNow,
            "Job", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64));
        var payload = JsonSerializer.Serialize(message, JsonSerializerOptions.Web);
        var result = new MatchingResult(Guid.NewGuid(), message.JobId, "deterministic-v2");
        result.Complete([new MatchingCriterion("Skills", 90m, 1m, "Verified test criterion.")]);

        Assert.True(await inbox.TryRecordProcessedAsync(message, message.GetPayloadSha256(payload), result));
        Assert.True(await inbox.IsProcessedAsync(message.EventId));
        Assert.False(await inbox.TryRecordProcessedAsync(message, message.GetPayloadSha256(payload), null));
        Assert.Equal(1, await db.JobPublishedEvents.CountAsync());
        Assert.Equal(1, await db.MatchingResults.CountAsync());
        Assert.Equal(1, await db.MatchingCriteria.CountAsync());

        var conflicting = message with { JobId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => inbox.TryRecordProcessedAsync(
            conflicting, conflicting.GetPayloadSha256(JsonSerializer.Serialize(conflicting, JsonSerializerOptions.Web)), null));
        Assert.Equal(1, await db.JobPublishedEvents.CountAsync());
        Assert.Equal(1, await db.MatchingResults.CountAsync());
    }
}
