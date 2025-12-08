namespace MaskBrowser.Server.Models;

public class Payment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentProvider Provider { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int? DepositRequestId { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? ProcessorTransactionId { get; set; }
    public string? ProcessorResponse { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}

public enum PaymentProvider
{
    CryptoBot,
    Bybit
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
}

