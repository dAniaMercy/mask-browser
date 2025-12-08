using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Services;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepositController : ControllerBase
{
    private readonly IDepositService _depositService;
    private readonly ILogger<DepositController> _logger;

    public DepositController(IDepositService depositService, ILogger<DepositController> logger)
    {
        _depositService = depositService;
        _logger = logger;
    }

    [HttpGet("methods")]
    [Authorize]
    public async Task<ActionResult<List<PaymentMethodDto>>> GetMethods()
    {
        var methods = await _depositService.GetEnabledMethodsAsync();
        return Ok(methods);
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<ActionResult<DepositRequestDto>> CreateDeposit([FromBody] CreateDepositRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _depositService.CreateDepositAsync(userId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Deposit creation validation failed");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deposit");
            return StatusCode(500, new { message = "Failed to create deposit" });
        }
    }

    [HttpGet("{depositId}/status")]
    [Authorize]
    public async Task<ActionResult<DepositStatusDto>> GetStatus(int depositId)
    {
        var userId = GetCurrentUserId();
        var status = await _depositService.GetStatusAsync(depositId, userId);

        if (status == null)
            return NotFound();

        return Ok(status);
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<ActionResult<PagedResult<DepositHistoryDto>>> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var history = await _depositService.GetUserHistoryAsync(userId, page, pageSize);
        return Ok(history);
    }

    [HttpPost("{depositId}/cancel")]
    [Authorize]
    public async Task<ActionResult> CancelDeposit(int depositId)
    {
        var userId = GetCurrentUserId();
        var success = await _depositService.CancelDepositAsync(depositId, userId);

        if (!success)
            return BadRequest(new { message = "Cannot cancel this deposit" });

        return Ok();
    }

    [HttpPost("webhook/{processorType}")]
    [AllowAnonymous]
    public async Task<ActionResult> ProcessWebhook(string processorType, [FromBody] JsonElement payload)
    {
        var signature = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
        await _depositService.ProcessWebhookAsync(processorType, payload, signature);
        return Ok();
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.Parse(claim?.Value ?? "0");
    }
}
