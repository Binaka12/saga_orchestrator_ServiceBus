namespace PaymentService.Models;

public class Payment
{
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PaymentRequest
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}
