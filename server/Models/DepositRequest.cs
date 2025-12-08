using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskBrowser.Server.Models;

public class DepositRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }

    [Required, MaxLength(50)]
    public string PaymentCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExpectedAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USDT";

    public int PaymentMethodId { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualAmount { get; set; }

    [MaxLength(200)]
    public string? TransactionId { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ProcessorResponse { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(PaymentMethodId))]
    public virtual PaymentMethod PaymentMethod { get; set; } = null!;
}

public enum DepositStatus
{
    Pending,
    Completed,
    Expired,
    Cancelled
}
