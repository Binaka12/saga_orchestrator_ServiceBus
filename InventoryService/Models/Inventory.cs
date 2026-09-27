namespace InventoryService.Models;

public class Inventory
{
    public string ProductId { get; set; } = string.Empty;
    public int Stock { get; set; }
}

public class InventoryRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
