using System.Net;
using System.Net.Http.Json;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Job.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Job.IntegrationTests.Api;

public sealed class CollectionPublicationsTests(JobApiFactory factory) : IClassFixture<JobApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/jobs/collection-publications";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static PublishCollectionCommand Command() => new(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64),
        new("Engineer", "Company", "Paris", WorkMode.Remote, ContractType.Permanent,
            50000, 60000, "EUR", "Description", "Manual", "https://example.invalid/1", DateTime.UtcNow), ["SQL"], null);

    [Fact]
    public async Task Publication_ShouldRejectRequestsWithoutPublisherCredential()
    {
        using var anonymous = factory.CreateUnauthenticatedClient();
        using var admin = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route, Command())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.PostAsJsonAsync(Route, Command())).StatusCode);
    }

    [Fact]
    public async Task ConcurrentPublication_ShouldCreateOneJob_AndPersistReceiptAndExtraMetadata()
    {
        using var client = factory.CreatePublisherClient();
        var command = Command();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(Route, command)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        var receipts = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<CollectionPublicationDto>()));
        Assert.Single(receipts.Select(x => x!.JobId).Distinct());
        foreach (var response in responses) response.Dispose();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        Assert.Equal(1, await db.Jobs.CountAsync());
        var saved = await db.CollectionPublications.SingleAsync(x => x.OfferId == command.OfferId);
        Assert.Contains("SQL", saved.RequestJson);
        var message = await db.OutboxMessages.SingleAsync(x => x.JobId == saved.JobId);
        using var envelope = System.Text.Json.JsonDocument.Parse(message.PayloadJson);
        Assert.Equal(JobPublishedV1.Type, message.EventType);
        Assert.Equal(message.Id, envelope.RootElement.GetProperty("eventId").GetGuid());
        Assert.Equal(saved.JobId, envelope.RootElement.GetProperty("jobId").GetGuid());
        Assert.Equal(saved.OfferId, envelope.RootElement.GetProperty("collectionOfferId").GetGuid());
        Assert.Equal(saved.RevisionId, envelope.RootElement.GetProperty("collectionRevisionId").GetGuid());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Retry_ShouldSurviveJobDeletion_AndRejectAnotherRevisionOrPayload()
    {
        using var client = factory.CreatePublisherClient();
        using var adminClient = factory.CreateClient();
        var command = Command();
        var created = await client.PostAsJsonAsync(Route, command);
        created.EnsureSuccessStatusCode();
        var receipt = (await created.Content.ReadFromJsonAsync<CollectionPublicationDto>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/jobs/{receipt.JobId}")).StatusCode);
        var retried = await client.PostAsJsonAsync(Route, command);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal(receipt, await retried.Content.ReadFromJsonAsync<CollectionPublicationDto>());
        using (var scope = factory.Services.CreateScope())
            Assert.Single(await scope.ServiceProvider.GetRequiredService<JobDbContext>().OutboxMessages.ToArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/jobs/{receipt.JobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { RevisionId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Route, command with { Job = command.Job with { Title = "Changed" } })).StatusCode);
    }

    [Theory]
    [InlineData("expired", HttpStatusCode.Conflict)]
    [InlineData("title", HttpStatusCode.BadRequest)]
    [InlineData("id", HttpStatusCode.BadRequest)]
    [InlineData("fingerprint", HttpStatusCode.BadRequest)]
    public async Task InvalidPublication_ShouldNotCreateJob(string field, HttpStatusCode expected)
    {
        using var client = factory.CreatePublisherClient();
        var command = Command();
        command = field switch
        {
            "expired" => command with { ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), Job = command.Job with { PublishedAtUtc = null } },
            "title" => command with { Job = command.Job with { Title = " " } },
            "id" => command with { OfferId = Guid.Empty },
            _ => command with { Fingerprint = "invalid" }
        };
        Assert.Equal(expected, (await client.PostAsJsonAsync(Route, command)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<JobDbContext>().Jobs.ToArrayAsync());
    }
}
