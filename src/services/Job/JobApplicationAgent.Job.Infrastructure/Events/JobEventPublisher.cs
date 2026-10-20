using System.Text.Json;
using JobApplicationAgent.Observability;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Domain.Entities;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace JobApplicationAgent.Job.Infrastructure.Events;

public sealed class JobEventPublisher : IJobEventPublisher
{
    private const string Exchange = "jobapplication.events";
    private const string DeadLetterExchange = "jobapplication.events.dlx";
    private const string Queue = "matching.job-published.v1";
    private const string DeadLetterQueue = "matching.job-published.v1.dead";
    private readonly ConnectionFactory factory;

    public JobEventPublisher(IConfiguration configuration)
    {
        var value = configuration["Job:EventTransport:RabbitMq:Uri"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("amqp" or "amqps") ||
            string.IsNullOrWhiteSpace(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.IsLoopback || string.Equals(uri.Host, "rabbitmq", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Configure a local AMQP URI with credentials in Job:EventTransport:RabbitMq:Uri.");
        factory = new ConnectionFactory { Uri = uri, ClientProvidedName = "JobApplicationAgent.Job.Outbox" };
    }

    public async Task PublishAsync(JobOutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.EventType != JobPublishedV1.Type || message.SchemaVersion != 1)
            throw new InvalidOperationException("Unsupported Job event contract.");
        using var payload = JsonDocument.Parse(message.PayloadJson, new JsonDocumentOptions { MaxDepth = 8 });
        if (payload.RootElement.GetProperty("eventId").GetGuid() != message.Id ||
            payload.RootElement.GetProperty("eventType").GetString() != message.EventType ||
            payload.RootElement.GetProperty("schemaVersion").GetInt32() != message.SchemaVersion ||
            payload.RootElement.GetProperty("jobId").GetGuid() != message.JobId)
            throw new InvalidOperationException("The Job outbox envelope does not match its persisted identity.");

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterQueue, arguments: null,
            cancellationToken: cancellationToken);
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterQueue
        };
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: queueArguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(Queue, Exchange, message.EventType, arguments: null,
            cancellationToken: cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.Id.ToString("D"), Type = message.EventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAtUtc).ToUnixTimeSeconds())
        };
        var headers = new Dictionary<string, object?>();
        if (message.CorrelationId is not null)
            headers["x-correlation-id"] = System.Text.Encoding.UTF8.GetBytes(message.CorrelationId);
        TraceContextHeaders.Inject(headers);
        if (headers.Count > 0) properties.Headers = headers;
        await channel.BasicPublishAsync(Exchange, message.EventType, mandatory: true, basicProperties: properties,
            body: System.Text.Encoding.UTF8.GetBytes(message.PayloadJson), cancellationToken: cancellationToken);
    }
}
