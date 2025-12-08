using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaskAdmin.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebhookController> _logger;
    private readonly IConfiguration _configuration;

    public WebhookController(
        ApplicationDbContext context,
        ILogger<WebhookController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Webhook endpoint for CryptoBot payments
    /// </summary>
    [HttpPost("cryptobot")]
    public async Task<IActionResult> CryptoBotWebhook([FromBody] CryptoBotWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received CryptoBot webhook: {UpdateId}", payload.UpdateId);

            // Verify webhook signature if configured
            var secret = _configuration["CryptoBot:WebhookSecret"];
            if (!string.IsNullOrEmpty(secret))
            {
                var signature = Request.Headers["X-Crypto-Bot-Signature"].FirstOrDefault();
                if (!VerifyCryptoBotSignature(payload, signature, secret))
                {
                    _logger.LogWarning("Invalid CryptoBot webhook signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }
            }

            // Check if payment already processed
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == payload.Payload.PaymentId.ToString());

            if (existingPayment != null)
            {
                _logger.LogInformation("Payment {PaymentId} already processed", payload.Payload.PaymentId);
                return Ok(new { status = "already_processed" });
            }

            // Find user by transaction metadata
            int userId;
            if (payload.Payload.Payload != null && int.TryParse(payload.Payload.Payload, out var parsedUserId))
            {
                userId = parsedUserId;
            }
            else
            {
                _logger.LogWarning("No user ID in CryptoBot webhook payload");
                return BadRequest(new { error = "Missing user ID" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for CryptoBot payment", userId);
                return NotFound(new { error = "User not found" });
            }

            // Create payment record
            var payment = new Payment
            {
                UserId = userId,
                Amount = payload.Payload.Amount,
                Currency = payload.Payload.Asset,
                Provider = PaymentProvider.CryptoBot,
                TransactionId = payload.Payload.PaymentId.ToString(),
                Status = PaymentStatus.Completed,
                Description = $"CryptoBot payment - {payload.Payload.Asset}",
                Metadata = JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            // Update user balance
            var oldBalance = user.Balance;
            user.Balance += payload.Payload.Amount;

            await _context.SaveChangesAsync();

            // Log the transaction
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = "PaymentReceived",
                Entity = "Payment",
                EntityId = payment.Id,
                Category = AuditLogCategory.PaymentManagement,
                Level = AuditLogLevel.Info,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "webhook",
                UserAgent = "CryptoBot Webhook",
                OldValues = oldBalance.ToString(),
                NewValues = user.Balance.ToString(),
                AdditionalData = JsonSerializer.Serialize(new
                {
                    amount = payload.Payload.Amount,
                    currency = payload.Payload.Asset,
                    transactionId = payload.Payload.PaymentId
                }),
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "CryptoBot payment processed: User {UserId}, Amount {Amount} {Currency}, New Balance {Balance}",
                userId, payload.Payload.Amount, payload.Payload.Asset, user.Balance);

            return Ok(new
            {
                status = "success",
                paymentId = payment.Id,
                userId = userId,
                amount = payment.Amount,
                newBalance = user.Balance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CryptoBot webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Webhook endpoint for Bybit payments
    /// </summary>
    [HttpPost("bybit")]
    public async Task<IActionResult> BybitWebhook([FromBody] BybitWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Received Bybit webhook: {OrderId}", payload.OrderId);

            // Verify webhook signature
            var secret = _configuration["Bybit:WebhookSecret"];
            if (!string.IsNullOrEmpty(secret))
            {
                var signature = Request.Headers["X-Bybit-Signature"].FirstOrDefault();
                if (!VerifyBybitSignature(payload, signature, secret))
                {
                    _logger.LogWarning("Invalid Bybit webhook signature");
                    return Unauthorized(new { error = "Invalid signature" });
                }
            }

            // Check if payment already processed
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.TransactionId == payload.OrderId);

            if (existingPayment != null)
            {
                _logger.LogInformation("Payment {OrderId} already processed", payload.OrderId);
                return Ok(new { status = "already_processed" });
            }

            // Find user by custom field
            if (!int.TryParse(payload.CustomUserId, out var userId))
            {
                _logger.LogWarning("No user ID in Bybit webhook payload");
                return BadRequest(new { error = "Missing user ID" });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for Bybit payment", userId);
                return NotFound(new { error = "User not found" });
            }

            // Map Bybit status to our status
            var status = payload.Status switch
            {
                "SUCCESS" => PaymentStatus.Completed,
                "PENDING" => PaymentStatus.Pending,
                "FAILED" => PaymentStatus.Failed,
                _ => PaymentStatus.Pending
            };

            // Create payment record
            var payment = new Payment
            {
                UserId = userId,
                Amount = payload.Amount,
                Currency = payload.Currency,
                Provider = PaymentProvider.Bybit,
                TransactionId = payload.OrderId,
                Status = status,
                Description = $"Bybit payment - {payload.Currency}",
                Metadata = JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow,
                CompletedAt = status == PaymentStatus.Completed ? DateTime.UtcNow : null,
                FailureReason = status == PaymentStatus.Failed ? payload.FailureReason : null
            };

            _context.Payments.Add(payment);

            // Update user balance only if payment is completed
            decimal oldBalance = user.Balance;
            if (status == PaymentStatus.Completed)
            {
                user.Balance += payload.Amount;
            }

            await _context.SaveChangesAsync();

            // Log the transaction
            if (status == PaymentStatus.Completed)
            {
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "PaymentReceived",
                    Entity = "Payment",
                    EntityId = payment.Id,
                    Category = AuditLogCategory.PaymentManagement,
                    Level = AuditLogLevel.Info,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "webhook",
                    UserAgent = "Bybit Webhook",
                    OldValues = oldBalance.ToString(),
                    NewValues = user.Balance.ToString(),
                    AdditionalData = JsonSerializer.Serialize(new
                    {
                        amount = payload.Amount,
                        currency = payload.Currency,
                        orderId = payload.OrderId
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Bybit payment processed: User {UserId}, Amount {Amount} {Currency}, Status {Status}, New Balance {Balance}",
                userId, payload.Amount, payload.Currency, status, user.Balance);

            return Ok(new
            {
                status = "success",
                paymentId = payment.Id,
                userId = userId,
                amount = payment.Amount,
                paymentStatus = status.ToString(),
                newBalance = user.Balance
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Bybit webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private bool VerifyCryptoBotSignature(CryptoBotWebhookPayload payload, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var expectedSignature = Convert.ToHexString(hash).ToLower();

            return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying CryptoBot signature");
            return false;
        }
    }

    private bool VerifyBybitSignature(BybitWebhookPayload payload, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        try
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
            var expectedSignature = Convert.ToBase64String(hash);

            return signature.Equals(expectedSignature, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Bybit signature");
            return false;
        }
    }
}

// CryptoBot webhook models
public class CryptoBotWebhookPayload
{
    public long UpdateId { get; set; }
    public CryptoBotPayload Payload { get; set; } = new();
}

public class CryptoBotPayload
{
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string? Payload { get; set; } // User ID stored here
}

// Bybit webhook models
public class BybitWebhookPayload
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CustomUserId { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}
