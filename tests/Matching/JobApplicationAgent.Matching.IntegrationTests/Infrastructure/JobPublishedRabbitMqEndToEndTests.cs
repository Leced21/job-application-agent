using System.Text;
using JobApplicationAgent.Job.Domain.Entities;
using JobApplicationAgent.Job.Domain.Enums;
using JobApplicationAgent.Job.Infrastructure.Events;
using JobApplicationAgent.Job.Infrastructure.Persistence;
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using Xunit;
using JobEntity = JobApplicationAgent.Job.Domain.Entities.Job;
using JobPublishedJob = JobApplicationAgent.Job.Application.Jobs.Create.JobPublishedV1;

namespace JobApplicationAgent.Matching.IntegrationTests.Infrastructure;

public sealed class JobPublishedRabbitMqEndToEndTests
{
    private static readonly Guid CandidateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task JobOutbox_ShouldReachMatchingOnce_AndDeadLetterMalformedEnvelope()
    {
        const string rabbitUser = "phase89test";
        const string rabbitPassword = "phase89test-password";
        await using var rabbit = new ContainerBuilder("rabbitmq:4-management")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", rabbitUser)
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", rabbitPassword)
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();
        await rabbit.StartAsync();

        await using var jobPostgres = CreatePostgres("job_event_test");
        await using var matchingPostgres = CreatePostgres("matching_event_test");
        await Task.WhenAll(jobPostgres.StartAsync(), matchingPostgres.StartAsync());

        var job = JobEntity.Create("Data Engineer", "Example Co", "Paris", WorkMode.Remote,
            ContractType.Permanent, null, null, null, "SQL and Python", "Test", "https://example.invalid/job", DateTime.UtcNow);

        var rabbitUri = new Uri($"amqp://{rabbitUser}:{rabbitPassword}@localhost:{rabbit.GetMappedPublicPort(5672)}/%2f");
        var jobOptions = new DbContextOptionsBuilder<JobDbContext>().UseNpgsql(jobPostgres.GetConnectionString()).Options;
        await using var jobDb = new JobDbContext(jobOptions);
        await jobDb.Database.MigrateAsync();

        using var matchingFactory = new MatchingConsumerFactory(
            matchingPostgres.GetConnectionString(), rabbitUri, job.Id);
        using var matchingClient = matchingFactory.CreateClient();
        using (var scope = matchingFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MatchingDbContext>().Database.MigrateAsync();

        var offerId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var receipt = CollectionPublication.Create(offerId, revisionId, job.Id, new string('C', 64),
            new string('D', 64), "{}", now);
        var outboxMessage = JobPublishedJob.CreateMessage(receipt);
        await using (var transaction = await jobDb.Database.BeginTransactionAsync())
        {
            jobDb.Jobs.Add(job);
            jobDb.CollectionPublications.Add(receipt);
            jobDb.OutboxMessages.Add(outboxMessage);
            await jobDb.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var jobConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Job:EventTransport:RabbitMq:Uri"] = rabbitUri.ToString()
        }).Build();
        var dispatcher = new JobOutboxDispatchService(jobDb, new JobEventPublisher(jobConfiguration), TimeProvider.System);
        var dispatchResult = await dispatcher.DispatchOneAsync(CancellationToken.None);
        Assert.NotNull(dispatchResult);
        Assert.True(dispatchResult.Published);

        await WaitUntilAsync(async () =>
        {
            await using var scope = matchingFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MatchingDbContext>();
            return await db.JobPublishedEvents.AnyAsync(x => x.EventId == outboxMessage.Id) &&
                   await db.MatchingResults.AnyAsync(x => x.JobId == job.Id);
        });

        await using var rawConnection = await new ConnectionFactory { Uri = rabbitUri }
            .CreateConnectionAsync(CancellationToken.None);
        await using var rawChannel = await rawConnection.CreateChannelAsync(cancellationToken: CancellationToken.None);
        await rawChannel.ExchangeDeclareAsync("jobapplication.events", ExchangeType.Topic, durable: true,
            autoDelete: false, arguments: null, cancellationToken: CancellationToken.None);
        var duplicateProperties = new BasicProperties
        {
            ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent,
            MessageId = outboxMessage.Id.ToString("D"), Type = JobPublishedJob.Type
        };
        await rawChannel.BasicPublishAsync("jobapplication.events", JobPublishedJob.Type, mandatory: true,
            basicProperties: duplicateProperties, body: Encoding.UTF8.GetBytes(outboxMessage.PayloadJson),
            cancellationToken: CancellationToken.None);
        await rawChannel.BasicPublishAsync("jobapplication.events", JobPublishedJob.Type, mandatory: true,
            basicProperties: new BasicProperties { ContentType = "application/json", Type = JobPublishedJob.Type, MessageId = Guid.NewGuid().ToString("D") },
            body: Encoding.UTF8.GetBytes("not-json"), cancellationToken: CancellationToken.None);

        BasicGetResult? deadLetter = null;
        await WaitUntilAsync(async () =>
        {
            deadLetter = await rawChannel.BasicGetAsync("matching.job-published.v1.dead", autoAck: true,
                cancellationToken: CancellationToken.None);
            return deadLetter is not null;
        });
        Assert.Equal("not-json", Encoding.UTF8.GetString(deadLetter!.Body.Span));

        await using var finalScope = matchingFactory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<MatchingDbContext>();
        Assert.Equal(1, await finalDb.JobPublishedEvents.CountAsync(x => x.EventId == outboxMessage.Id));
        Assert.Equal(1, await finalDb.MatchingResults.CountAsync(x => x.JobId == job.Id));
    }

    private static PostgreSqlContainer CreatePostgres(string database) => new PostgreSqlBuilder("postgres:17")
        .WithDatabase(database).WithUsername("phase89").WithPassword("phase89-test-password").Build();

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!timeout.IsCancellationRequested)
        {
            if (await condition()) return;
            await Task.Delay(200, timeout.Token);
        }
        throw new TimeoutException("Timed out waiting for the RabbitMQ event consumer.");
    }

    private sealed class MatchingConsumerFactory(string connectionString, Uri rabbitUri, Guid jobId)
        : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MatchingDb", connectionString);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MatchingDb"] = connectionString,
                    ["Services:Job:BaseUrl"] = "http://localhost:5002/",
                    ["Services:Profile:BaseUrl"] = "http://localhost:5001/",
                    ["Matching:EventTransport:Enabled"] = "true",
                    ["Matching:EventTransport:RabbitMq:Uri"] = rabbitUri.ToString()
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IJobMatchingDataClient>();
                services.RemoveAll<IProfileMatchingDataClient>();
                services.AddSingleton<IJobMatchingDataClient>(new FixedJobClient(jobId));
                services.AddSingleton<IProfileMatchingDataClient>(new FixedProfileClient());
            });
        }
    }

    private sealed class FixedJobClient(Guid jobId) : IJobMatchingDataClient
    {
        public Task<JobMatchingData?> GetByIdAsync(Guid requestedId, CancellationToken cancellationToken = default) =>
            Task.FromResult<JobMatchingData?>(requestedId == jobId
                ? new JobMatchingData(jobId, "Data Engineer", "Example Co", "Paris", "Remote", "Permanent",
                    null, null, null, "SQL and Python")
                : null);

        public Task<IReadOnlyList<JobMatchingData>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JobMatchingData>>([]);

        public Task<IReadOnlyList<JobMatchingData>> GetRecommendablePageAsync(int page, int pageSize,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JobMatchingData>>([]);
    }

    private sealed class FixedProfileClient : IProfileMatchingDataClient
    {
        public Task<ProfileMatchingData?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ProfileMatchingData?>(new ProfileMatchingData(CandidateId, "Data Engineer",
                "SQL and Python experience", [], [new MatchingSkillData("SQL", "Data", "Advanced", 4)],
                [], [], [], null));
    }
}
