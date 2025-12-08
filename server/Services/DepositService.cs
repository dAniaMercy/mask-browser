using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskBrowser.Server.Services;

public interface IDepositService
{
    Task<List<PaymentMethodDto>> GetEnabledMethodsAsync();
    Task<DepositRequestDto> CreateDepositAsync(int userId, CreateDepositRequestDto request);
    Task<DepositStatusDto?> GetStatusAsync(int depositId, int userId);
    Task<PagedResult<DepositHistoryDto>> GetUserHistoryAsync(int userId, int page, int pageSize);
    Task<bool> CancelDepositAsync(int depositId, int userId);
    Task ProcessWebhookAsync(string processorType, JsonElement payload, string? signature);
}

public class DepositService : IDepositService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DepositService> _logger;
    private readonly IConfiguration _configuration;

    public DepositService(
        ApplicationDbContext context,
        ILogger<DepositService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<PaymentMethodDto>> GetEnabledMethodsAsync()
    {
        var methods = await _context.PaymentMethods
            .Where(m => m.IsEnabled)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        return methods.Select(m => new PaymentMethodDto(
            m.Id,
            m.Name,
            m.DisplayName,
            m.Description,
            m.IconUrl,
            m.QrCodeUrl,
            m.RedirectUrl,
            m.MinAmount,
            m.MaxAmount,
            m.Currency,
            m.FeePercent,
            m.FeeFixed)).ToList();
    }

    public async Task<DepositRequestDto> CreateDepositAsync(int userId, CreateDepositRequestDto request)
    {
        var method = await _context.PaymentMethods
            .FirstOrDefaultAsync(m => m.Id == request.PaymentMethodId && m.IsEnabled);

        if (method == null)
            throw new ArgumentException("Payment method not found or disabled");

        if (request.Amount.HasValue)
        {
            if (request.Amount < method.MinAmount || request.Amount > method.MaxAmount)
                throw new ArgumentException($"Amount must be between {method.MinAmount} and {method.MaxAmount}");
        }

        var paymentCode = GeneratePaymentCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(method.CodeExpirationMinutes);

        var deposit = new DepositRequest
        {
            UserId = userId,
            PaymentCode = paymentCode,
            ExpectedAmount = request.Amount,
            Currency = method.Currency,
            PaymentMethodId = method.Id,
            Status = "pending",
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _context.DepositRequests.Add(deposit);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deposit request created: {DepositId} for user {UserId} with code {PaymentCode}",
            deposit.Id, userId, paymentCode);

        return new DepositRequestDto(
            deposit.Id,
            paymentCode,
            expiresAt,
            method.Description ?? "Follow the instructions to complete payment",
            method.QrCodeUrl,
            method.RedirectUrl,
            request.Amount,
            method.Currency);
    }

    public async Task<DepositStatusDto?> GetStatusAsync(int depositId, int userId)
    {
        var deposit = await _context.DepositRequests
            .FirstOrDefaultAsync(d => d.Id == depositId && d.UserId == userId);

        if (deposit == null)
            return null;

        var secondsRemaining = Math.Max(0, (int)(deposit.ExpiresAt - DateTime.UtcNow).TotalSeconds);

        return new DepositStatusDto(
            deposit.Id,
            deposit.Status,
            deposit.ActualAmount,
            deposit.CompletedAt,
            secondsRemaining);
    }

    public async Task<PagedResult<DepositHistoryDto>> GetUserHistoryAsync(int userId, int page, int pageSize)
    {
        var query = _context.DepositRequests
            .Include(d => d.PaymentMethod)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DepositHistoryDto(
                d.Id,
                d.PaymentCode,
                d.PaymentMethod.DisplayName,
                d.PaymentMethod.IconUrl ?? string.Empty,
                d.ActualAmount ?? d.ExpectedAmount ?? 0,
                d.Currency,
                d.Status,
                d.CreatedAt,
                d.CompletedAt))
            .ToListAsync();

        return new PagedResult<DepositHistoryDto>(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<bool> CancelDepositAsync(int depositId, int userId)
    {
        var deposit = await _context.DepositRequests
            .FirstOrDefaultAsync(d => d.Id == depositId && d.UserId == userId && d.Status == "pending");

        if (deposit == null)
            return false;

        deposit.Status = "cancelled";
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deposit {DepositId} cancelled by user {UserId}", depositId, userId);
        return true;
    }

    public async Task ProcessWebhookAsync(string processorType, JsonElement payload, string? signature)
    {
        var secret = _configuration[$"Webhooks:{processorType}:Secret"];
        if (!VerifySignature(payload, signature, secret))
        {
            _logger.LogWarning("Invalid webhook signature for processor {ProcessorType}", processorType);
            throw new UnauthorizedAccessException("Invalid signature");
        }

        if (!payload.TryGetProperty("event", out var eventProp))
            throw new ArgumentException("Invalid payload");

        var eventType = eventProp.GetString();
        if (eventType == "deposit.completed")
        {
            var depositId = payload.GetProperty("deposit_id").GetInt32();
            var amount = decimal.Parse(payload.GetProperty("amount").GetString() ?? "0");
            var transactionId = payload.TryGetProperty("transaction_id", out var tx) ? tx.GetString() : null;

            await CompleteDepositAsync(depositId, amount, transactionId, payload.ToString(), processorType);
        }
    }

    private async Task CompleteDepositAsync(int depositId, decimal amount, string? transactionId, string response, string processorType)
    {
        var deposit = await _context.DepositRequests
            .Include(d => d.User)
            .Include(d => d.PaymentMethod)
            .FirstOrDefaultAsync(d => d.Id == depositId && d.Status == "pending");

        if (deposit == null)
        {
            _logger.LogWarning("Deposit {DepositId} not found or already processed", depositId);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            deposit.Status = "completed";
            deposit.ActualAmount = amount;
            deposit.TransactionId = transactionId;
            deposit.ProcessorResponse = response;
            deposit.CompletedAt = DateTime.UtcNow;

            deposit.User.Balance += amount;

            var payment = new Payment
            {
                UserId = deposit.UserId,
                Amount = amount,
                Currency = deposit.Currency,
                Provider = MapProvider(processorType),
                TransactionId = transactionId ?? string.Empty,
                Status = PaymentStatus.Completed,
                CompletedAt = DateTime.UtcNow,
                Description = $"Deposit via {deposit.PaymentMethod?.Name ?? processorType}",
                ProcessorResponse = response,
                ProcessorTransactionId = transactionId,
                PaymentMethodId = deposit.PaymentMethodId,
                DepositRequestId = deposit.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Deposit {DepositId} completed: {Amount} {Currency} for user {UserId}",
                depositId, amount, deposit.Currency, deposit.UserId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error completing deposit {DepositId}", depositId);
            throw;
        }
    }

    private static PaymentProvider MapProvider(string processorType)
    {
        return processorType.ToLowerInvariant() switch
        {
            "cryptobot" => PaymentProvider.CryptoBot,
            "bybit" => PaymentProvider.Bybit,
            _ => PaymentProvider.CryptoBot
        };
    }

    private static string GeneratePaymentCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new byte[6];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);

        var code = new char[6];
        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[random[i] % chars.Length];
        }

        return $"MASK-{new string(code)}";
    }

    private static bool VerifySignature(JsonElement payload, string? signature, string? secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            return false;

        var message = payload.ToString() ?? string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();

        return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
    }
}
