using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace PaymentService.Services;

public class PaymentCommandProcessor : IHostedService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private ServiceBusProcessor? _processor;
    private readonly ILogger<PaymentCommandProcessor> _logger;

    // Simple in-memory payments store
    private static readonly List<PaymentRecord> Payments = new();

    public PaymentCommandProcessor(ServiceBusClient client, ILogger<PaymentCommandProcessor> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = _client.CreateProcessor("payments-commands", new ServiceBusProcessorOptions
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
        _logger.LogInformation("Payments received message. CorrelationId={CorrelationId} Body={Body}", args.Message.CorrelationId, body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Command", out var cmd))
            {
                _logger.LogWarning("Payment command missing 'Command' property.");
                await args.AbandonMessageAsync(args.Message);
                return;
            }

            var command = cmd.GetString();

            if (string.Equals(command, "ProcessPayment", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling ProcessPayment command");

                // Expect OrderId and Amount at top level
                var orderId = root.TryGetProperty("OrderId", out var oid) && oid.TryGetInt32(out var o) ? o : 0;
                var amount = root.TryGetProperty("Amount", out var amt) && amt.TryGetDecimal(out var a) ? a : 0m;

                // Simulate processing (call gateway here)
                var payment = new PaymentRecord
                {
                    Id = Payments.Count + 1,
                    OrderId = orderId,
                    Amount = amount,
                    Status = "Processed",
                    ProcessedAt = DateTime.UtcNow
                };

                Payments.Add(payment);
                _logger.LogInformation("Payment processed. PaymentId={PaymentId} OrderId={OrderId} Amount={Amount}", payment.Id, payment.OrderId, payment.Amount);

                // Publish PaymentProcessed event
                try
                {
                    var sender = _client.CreateSender("payments-events");
                    var evt = new { Event = "PaymentProcessed", Payment = payment };
                    var msg = new ServiceBusMessage(JsonSerializer.Serialize(evt))
                    {
                        ContentType = "application/json",
                        CorrelationId = args.Message.CorrelationId
                    };
                    await sender.SendMessageAsync(msg);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish PaymentProcessed event (non-fatal)");
                }
            }
            else if (string.Equals(command, "CancelPayment", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling CancelPayment command");

                var orderId = root.TryGetProperty("OrderId", out var oid2) && oid2.TryGetInt32(out var o2) ? o2 : 0;

                var payment = Payments.Find(p => p.OrderId == orderId && p.Status == "Processed");
                if (payment != null)
                {
                    payment.Status = "Cancelled";
                    payment.CancelledAt = DateTime.UtcNow;
                    _logger.LogInformation("Payment cancelled. PaymentId={PaymentId} OrderId={OrderId}", payment.Id, payment.OrderId);

                    // Publish PaymentCancelled event
                    try
                    {
                        var sender = _client.CreateSender("payments-events");
                        var evt = new { Event = "PaymentCancelled", Payment = payment };
                        var msg = new ServiceBusMessage(JsonSerializer.Serialize(evt))
                        {
                            ContentType = "application/json",
                            CorrelationId = args.Message.CorrelationId
                        };
                        await sender.SendMessageAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish PaymentCancelled event (non-fatal)");
                    }
                }
                else
                {
                    _logger.LogWarning("No matching processed payment found to cancel for OrderId={OrderId}", orderId);
                }
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment command");
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

    private class PaymentRecord
    {
        public int Id { get; init; }
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
    }
}