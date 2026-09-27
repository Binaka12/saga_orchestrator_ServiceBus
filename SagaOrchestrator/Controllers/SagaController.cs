using Microsoft.AspNetCore.Mvc;
using SagaOrchestrator.Models;
using SagaOrchestrator.Services;
using System;

namespace SagaOrchestrator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SagaController : ControllerBase
{
    private readonly IMessagePublisher _publisher;

    public SagaController(IMessagePublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<IActionResult> ProcessOrder([FromBody] Order order)
    {
        // Initialize saga state
        order.Status = "Pending";
        var correlationId = Guid.NewGuid().ToString();

        // Track which steps were successfully published so we can send compensating commands on failure
        var createdOrderPublished = false;
        var reservedInventoryPublished = false;
        var paymentPublished = false;

        try
        {
            // 1) Publish CreateOrder command
            await _publisher.SendMessageAsync(
                "orders-commands",
                new { Command = "CreateOrder", Order = order },
                correlationId);
            createdOrderPublished = true;

            // 2) Publish ReserveInventory command
            await _publisher.SendMessageAsync(
                "inventory-commands",
                new { Command = "Reserve", OrderId = order.Id, ProductId = order.ProductId, Quantity = order.Quantity },
                correlationId);
            reservedInventoryPublished = true;

            // 3) Publish ProcessPayment command
            await _publisher.SendMessageAsync(
                "payments-commands",
                new { Command = "ProcessPayment", OrderId = order.Id, Amount = order.Quantity * 10m },
                correlationId);
            paymentPublished = true;

            // All commands published successfully — orchestration proceeds asynchronously
            return Accepted(new { Message = "Saga started", CorrelationId = correlationId, OrderId = order.Id });
        }
        catch (Exception ex)
        {
            // Something went wrong while publishing commands — attempt compensating commands for previously published steps.
            // Note: publishing compensations is best-effort.
            try
            {
                if (paymentPublished)
                {
                    // If payment command was published and we detect an error afterward, try to cancel payment (compensation)
                    await _publisher.SendMessageAsync("payments-commands", new { Command = "CancelPayment", OrderId = order.Id }, correlationId);
                }

                if (reservedInventoryPublished)
                {
                    // Release inventory
                    await _publisher.SendMessageAsync("inventory-commands", new { Command = "Release", OrderId = order.Id, ProductId = order.ProductId, Quantity = order.Quantity }, correlationId);
                }

                if (createdOrderPublished)
                {
                    // Cancel order
                    await _publisher.SendMessageAsync("orders-commands", new { Command = "CancelOrder", OrderId = order.Id }, correlationId);
                }
            }
            catch
            {
                // Swallow compensating errors — they will be retried/handled by consumers if needed.
            }

            // Return failure to the caller with minimal details (don't leak internal exceptions)
            return BadRequest(new { Message = "Failed to start saga. See server logs for details." });
        }
    }
}