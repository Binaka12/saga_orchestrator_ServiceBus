using Microsoft.AspNetCore.Mvc;
using InventoryService.Models;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private static readonly List<Inventory> Inventories = new()
    {
        new Inventory { ProductId = "Product1", Stock = 100 },
        new Inventory { ProductId = "Product2", Stock = 200 }
    };

    [HttpPost("reserve")]
    public IActionResult ReserveInventory([FromBody] InventoryRequest request)
    {
        var inventory = Inventories.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (inventory is null || inventory.Stock < request.Quantity)
            return BadRequest("Insufficient stock.");

        inventory.Stock -= request.Quantity;
        return Ok(new { message = "Inventory reserved.", productId = request.ProductId, remainingStock = inventory.Stock });
    }

    [HttpPost("release")]
    public IActionResult ReleaseInventory([FromBody] InventoryRequest request)
    {
        var inventory = Inventories.FirstOrDefault(i => i.ProductId == request.ProductId);
        if (inventory is null)
            return NotFound();

        inventory.Stock += request.Quantity;
        return Ok(new { message = "Inventory released.", productId = request.ProductId, stock = inventory.Stock });
    }

    [HttpGet]
    public IActionResult GetInventory() => Ok(Inventories);
}
