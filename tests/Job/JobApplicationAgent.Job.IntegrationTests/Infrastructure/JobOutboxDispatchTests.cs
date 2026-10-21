using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Events;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Job.IntegrationTests.Infrastructure;

public sealed class JobOutboxDispatchTests(JobApiFactory factory) : IClassFixture<JobApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();

    [Fact]
    public async Task DispatchOneAsync_ShouldPublishAndAcknowledgeOnlyOnce()
    {
        using var client = factory.CreatePublisherClient();
        var command = CreateCommand();
        var response = await client.PostAsJsonAsync("/api/v1/jobs/collection-publications", command);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var publisher = new RecordingPublisher();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        var dispatcher = new JobOutboxDispatchService(db, publisher, TimeProvider.System);

        var dispatched = await dispatcher.DispatchOneAsync(CancellationToken.None);
        var empty = await dispatcher.DispatchOneAsync(CancellationToken.None);

        Assert.NotNull(dispatched);
        Assert.True(dispatched.Published);
        Assert.Null(empty);
        Assert.Single(publisher.Messages);
        var persisted = await db.OutboxMessages.SingleAsync();
        Assert.Equal(publisher.Messages[0].Id, persisted.Id);
        Assert.NotNull(persisted.DispatchedAtUtc);
        Assert.Equal(0, persisted.AttemptCount);
    }

    private static PublishCollectionCommand CreateCommand() => new(Guid.NewGuid(), Guid.NewGuid(), new string('B', 64),
        new("Engineer", "Company", "Paris", WorkMode.Remote, ContractType.Permanent,
            50000, 60000, "EUR", "Description", "Manual", "https://example.invalid/outbox", DateTime.UtcNow), ["SQL"], null);

    private sealed class RecordingPublisher : IJobEventPublisher
    {
        public List<JobApplicationAgent.Job.Domain.Entities.JobOutboxMessage> Messages { get; } = [];

        public Task PublishAsync(JobApplicationAgent.Job.Domain.Entities.JobOutboxMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
