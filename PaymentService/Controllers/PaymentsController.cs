using Microsoft.AspNetCore.Mvc;
using PaymentService.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private static readonly List<Payment> Payments = new();

    [HttpPost]
    public IActionResult ProcessPayment([FromBody] PaymentRequest request)
    {
        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = "Processed"
        };

        Payments.Add(payment);
        return Ok(payment);
    }

    [HttpGet]
    public IActionResult GetPayments() => Ok(Payments);
}
