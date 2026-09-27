using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Models;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace OrderService.Services;

public class OrderCommandProcessor : IHostedService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private ServiceBusProcessor? _processor;
    private readonly ILogger<OrderCommandProcessor> _logger;
    private readonly IMessageDeduplicator _messageDeduplicator;

    // Simple in-memory store (keeps parity with the existing HTTP controller sample)
    private static readonly List<Order> Orders = new();

    public OrderCommandProcessor(ServiceBusClient client, ILogger<OrderCommandProcessor> logger, IMessageDeduplicator messageDeduplicator)
    {
        _client = client;
        _logger = logger;
        _messageDeduplicator = messageDeduplicator;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = _client.CreateProcessor("orders-commands", new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 2
        });

        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;

        return _processor.StartProcessingAsync(cancellationToken);
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        _logger.LogInformation("Received message. CorrelationId={CorrelationId} Body={Body}", args.Message.CorrelationId, body);

        var messageId = args.Message.MessageId ?? args.Message.CorrelationId ?? Guid.NewGuid().ToString();
        if (!_messageDeduplicator.TryMarkProcessed(messageId))
        {
            _logger.LogWarning("Duplicate message received; skipping. MessageId={MessageId}", messageId);
            await args.CompleteMessageAsync(args.Message);
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("Command", out var cmd))
            {
                var command = cmd.GetString();

                if (string.Equals(command, "CreateOrder", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Handling CreateOrder command");

                    if (root.TryGetProperty("Order", out var orderElement))
                    {
                        // Deserialize incoming order payload to Order model
                        var order = JsonSerializer.Deserialize<Order>(orderElement.GetRawText()) ?? throw new InvalidOperationException("Invalid order payload");

                        // Persist to in-memory store (assign Id/status similar to HTTP controller)
                        order.Id = Orders.Count + 1;
                        order.Status = "Created";
                        Orders.Add(order);

                        _logger.LogInformation("Order created. Id={OrderId}", order.Id);

                        // Publish an event (optional) to notify other services
                        try
                        {
                            var sender = _client.CreateSender("orders-events");
                            var evt = new { Event = "OrderCreated", Order = order };
                            var msg = new ServiceBusMessage(JsonSerializer.Serialize(evt))
                            {
                                ContentType = "application/json",
                                CorrelationId = args.Message.CorrelationId
                            };
                            await sender.SendMessageAsync(msg);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to publish OrderCreated event (non-fatal)");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("CreateOrder command received without an 'Order' payload.");
                    }
                }
                else if (string.Equals(command, "CancelOrder", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Handling CancelOrder command");
                    if (root.TryGetProperty("OrderId", out var idElem) && idElem.TryGetInt32(out var id))
                    {
                        var existing = Orders.Find(o => o.Id == id);
                        if (existing != null)
                        {
                            Orders.Remove(existing);
                            _logger.LogInformation("Order cancelled. Id={OrderId}", id);
                        }
                    }
                }
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Service Bus error: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
            _processor = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor != null) await _processor.DisposeAsync();
    }
}