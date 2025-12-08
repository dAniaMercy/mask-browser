using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Services;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly CryptoPaymentService _cryptoPaymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        CryptoPaymentService cryptoPaymentService,
        ILogger<PaymentController> logger)
    {
        _cryptoPaymentService = cryptoPaymentService;
        _logger = logger;
    }

    [HttpPost("verify/cryptobot")]
    public async Task<IActionResult> VerifyCryptoBotPayment([FromBody] VerifyPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InvoiceId))
        {
            return BadRequest(new { error = "InvoiceId is required" });
        }
        var result = await _cryptoPaymentService.VerifyCryptoBotPaymentAsync(request.InvoiceId);
        return Ok(new { verified = result });
    }

    [HttpPost("verify/bybit")]
    public async Task<IActionResult> VerifyBybitPayment([FromBody] VerifyPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return BadRequest(new { error = "OrderId is required" });
        }
        var result = await _cryptoPaymentService.VerifyBybitPaymentAsync(request.OrderId);
        return Ok(new { verified = result });
    }
}

public class VerifyPaymentRequest
{
    public string? InvoiceId { get; set; }
    public string? OrderId { get; set; }
}

