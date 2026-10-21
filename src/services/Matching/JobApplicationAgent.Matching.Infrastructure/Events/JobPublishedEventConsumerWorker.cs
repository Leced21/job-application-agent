using System.Diagnostics;
using System.Text;
using JobApplicationAgent.Observability;
using JobApplicationAgent.Matching.Application.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace JobApplicationAgent.Matching.Infrastructure.Events;

public sealed class JobPublishedEventConsumerWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<JobPublishedEventConsumerWorker> logger) : BackgroundService
{
    private const string Exchange = "jobapplication.events";
    private const string DeadLetterExchange = "jobapplication.events.dlx";
    private const string Queue = "matching.job-published.v1";
    private const string DeadLetterQueue = "matching.job-published.v1.dead";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Matching:EventTransport:Enabled")) return;
        var delaySeconds = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = CreateConnectionFactory();
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await DeclareTopologyAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, stoppingToken);
                await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, stoppingToken);
                delaySeconds = 1;
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning("Matching event consumer stopped ({FailureType}); reconnecting after {DelaySeconds}s.",
                    exception.GetType().Name, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                delaySeconds = Math.Min(delaySeconds * 2, 30);
            }
        }
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        var startedAt = EventTransportMetrics.StartMeasurement();
        var outcome = "processed";
        var parentContext = TraceContextHeaders.Extract(delivery.BasicProperties.Headers);
        using var activity = EventTransportMetrics.ActivitySource.StartActivity(
            "matching.job-published.process", ActivityKind.Consumer, parentContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", Queue);
        activity?.SetTag("messaging.operation.type", "process");
        using var logScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = ReadCorrelationId(delivery.BasicProperties.Headers) ?? "unknown"
        });
        try
        {
            if (delivery.BasicProperties.Type != JobPublishedV1.Type)
                throw new InvalidOperationException("Unsupported event type.");
            var payload = Encoding.UTF8.GetString(delivery.Body.Span);
            var message = JobPublishedV1.ParseAndValidate(payload);
            if (!Guid.TryParse(delivery.BasicProperties.MessageId, out var messageId) || messageId != message.EventId)
                throw new InvalidOperationException("AMQP message identity does not match its event envelope.");

            using var scope = scopes.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<JobPublishedEventProcessor>();
            await processor.ProcessAsync(message, payload, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
        }
        catch (InvalidOperationException exception)
        {
            outcome = "rejected";
            activity?.SetTag("messaging.outcome", outcome);
            activity?.SetStatus(ActivityStatusCode.Error, "invalid_message");
            activity?.AddEvent(new ActivityEvent("messaging.delivery.rejected",
                tags: new ActivityTagsCollection { { "error.type", exception.GetType().Name } }));
            logger.LogWarning("Rejecting invalid Job event ({FailureType}).", exception.GetType().Name);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (System.Text.Json.JsonException exception)
        {
            outcome = "rejected";
            activity?.SetTag("messaging.outcome", outcome);
            activity?.SetStatus(ActivityStatusCode.Error, "malformed_message");
            activity?.AddEvent(new ActivityEvent("messaging.delivery.rejected",
                tags: new ActivityTagsCollection { { "error.type", exception.GetType().Name } }));
            logger.LogWarning("Rejecting malformed Job event ({FailureType}).", exception.GetType().Name);
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (Exception exception)
        {
            outcome = "retry";
            activity?.SetTag("messaging.outcome", outcome);
            activity?.SetStatus(ActivityStatusCode.Error, "processing_failed");
            activity?.AddEvent(new ActivityEvent("messaging.delivery.retry",
                tags: new ActivityTagsCollection { { "error.type", exception.GetType().Name } }));
            logger.LogWarning("Job event processing failed ({FailureType}); delivery will be retried.", exception.GetType().Name);
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
        finally
        {
            activity?.SetTag("messaging.outcome", outcome);
            if (!cancellationToken.IsCancellationRequested)
                EventTransportMetrics.RecordConsumerDelivery(startedAt, "matching.job-published.v1", outcome);
        }
    }

    private static string? ReadCorrelationId(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue("x-correlation-id", out var value)) return null;
        var text = value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string stringValue => stringValue,
            _ => null
        };
        return Guid.TryParse(text, out var id) ? id.ToString("D") : null;
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        var value = configuration["Matching:EventTransport:RabbitMq:Uri"];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("amqp" or "amqps") ||
            string.IsNullOrWhiteSpace(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            !(uri.IsLoopback || string.Equals(uri.Host, "rabbitmq", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Configure a local AMQP URI with credentials in Matching:EventTransport:RabbitMq:Uri.");
        return new ConnectionFactory { Uri = uri, ClientProvidedName = "JobApplicationAgent.Matching.Consumer" };
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
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
        await channel.QueueBindAsync(Queue, Exchange, JobPublishedV1.Type, arguments: null,
            cancellationToken: cancellationToken);
    }
}
