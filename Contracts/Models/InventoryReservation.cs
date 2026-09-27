namespace Contracts.Models;

public class InventoryReservation
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
}