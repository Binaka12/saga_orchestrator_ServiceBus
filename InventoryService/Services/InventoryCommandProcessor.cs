using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace InventoryService.Services;

public class InventoryCommandProcessor : IHostedService, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private ServiceBusProcessor? _processor;
    private readonly ILogger<InventoryCommandProcessor> _logger;

    // Simple in-memory reservations store
    private static readonly List<InventoryReservation> Reservations = new();

    public InventoryCommandProcessor(ServiceBusClient client, ILogger<InventoryCommandProcessor> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = _client.CreateProcessor("inventory-commands", new ServiceBusProcessorOptions
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
        _logger.LogInformation("Inventory received message. CorrelationId={CorrelationId} Body={Body}", args.Message.CorrelationId, body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Command", out var cmd))
            {
                _logger.LogWarning("Inventory command missing 'Command' property.");
                await args.AbandonMessageAsync(args.Message);
                return;
            }

            var command = cmd.GetString();

            if (string.Equals(command, "Reserve", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling Reserve command");

                var orderId = root.TryGetProperty("OrderId", out var oid) && oid.TryGetInt32(out var o) ? o : 0;
                var productId = root.TryGetProperty("ProductId", out var pid) ? pid.GetString() ?? string.Empty : string.Empty;
                var quantity = root.TryGetProperty("Quantity", out var q) && q.TryGetInt32(out var qi) ? qi : 0;

                // Simulate reservation logic
                var reservation = new InventoryReservation
                {
                    Id = Reservations.Count + 1,
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = quantity,
                    Status = "Reserved",
                    ReservedAt = DateTime.UtcNow
                };

                Reservations.Add(reservation);
                _logger.LogInformation("Inventory reserved. ReservationId={ReservationId} OrderId={OrderId} Product={ProductId} Qty={Qty}", reservation.Id, orderId, productId, quantity);

                // Publish InventoryReserved event
                try
                {
                    var sender = _client.CreateSender("inventory-events");
                    var evt = new { Event = "InventoryReserved", Reservation = reservation };
                    var msg = new ServiceBusMessage(JsonSerializer.Serialize(evt))
                    {
                        ContentType = "application/json",
                        CorrelationId = args.Message.CorrelationId
                    };
                    await sender.SendMessageAsync(msg);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish InventoryReserved event (non-fatal)");
                }
            }
            else if (string.Equals(command, "Release", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Handling Release command");

                var orderId = root.TryGetProperty("OrderId", out var oid2) && oid2.TryGetInt32(out var o2) ? o2 : 0;
                var productId = root.TryGetProperty("ProductId", out var pid2) ? pid2.GetString() ?? string.Empty : string.Empty;
                var quantity = root.TryGetProperty("Quantity", out var q2) && q2.TryGetInt32(out var qi2) ? qi2 : 0;

                // Find reservation(s) and release
                var matching = Reservations.Find(r => r.OrderId == orderId && r.ProductId == productId && r.Status == "Reserved");
                if (matching != null)
                {
                    matching.Status = "Released";
                    matching.ReleasedAt = DateTime.UtcNow;
                    _logger.LogInformation("Inventory released. ReservationId={ReservationId} OrderId={OrderId}", matching.Id, orderId);

                    // Publish InventoryReleased event
                    try
                    {
                        var sender = _client.CreateSender("inventory-events");
                        var evt = new { Event = "InventoryReleased", Reservation = matching, ReleasedQuantity = quantity };
                        var msg = new ServiceBusMessage(JsonSerializer.Serialize(evt))
                        {
                            ContentType = "application/json",
                            CorrelationId = args.Message.CorrelationId
                        };
                        await sender.SendMessageAsync(msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish InventoryReleased event (non-fatal)");
                    }
                }
                else
                {
                    _logger.LogWarning("No matching reservation found to release for OrderId={OrderId} ProductId={ProductId}", orderId, productId);
                }
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing inventory command");
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

    private class InventoryReservation
    {
        public int Id { get; init; }
        public int OrderId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
    }
}