using Microsoft.AspNetCore.Mvc;
using OrderService.Models;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> Orders = new();

    [HttpPost]
    public IActionResult CreateOrder([FromBody] Order order)
    {
        order.Id = Orders.Count + 1;
        order.Status = "Created";
        Orders.Add(order);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpDelete("{id:int}")]
    public IActionResult CancelOrder(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        if (order is null)
            return NotFound();

        Orders.Remove(order);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public IActionResult GetOrderById(int id)
    {
        var order = Orders.FirstOrDefault(o => o.Id == id);
        return order is null ? NotFound() : Ok(order);
    }
}
