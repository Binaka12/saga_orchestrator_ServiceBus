using Azure.Messaging.ServiceBus;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace SagaOrchestrator.Services;

public class ServiceBusMessagePublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ServiceBusMessagePublisher(ServiceBusClient client)
    {
        _client = client;

        // Simple retry: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4)
            });
    }

    public async Task SendMessageAsync<T>(string queueOrTopic, T payload, string? correlationId = null, IDictionary<string, object>? applicationProperties = null)
    {
        var sender = _client.CreateSender(queueOrTopic);
        var json = JsonSerializer.Serialize(payload);
        var message = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
        };

        if (applicationProperties != null)
        {
            foreach (var kv in applicationProperties)
                message.ApplicationProperties[kv.Key] = kv.Value!;
        }

        // Retry on transient failures
        await _retryPolicy.ExecuteAsync(async () =>
        {
            await sender.SendMessageAsync(message);
        });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}