using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskBrowser.Server.Models;

public class PaymentMethod
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? RedirectUrl { get; set; }

    [Required, MaxLength(50)]
    public string ProcessorType { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string ProcessorConfig { get; set; } = "{}";

    public string? WebhookUrl { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinAmount { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MaxAmount { get; set; } = 10000;

    [MaxLength(10)]
    public string Currency { get; set; } = "USDT";

    [Column(TypeName = "decimal(5,2)")]
    public decimal FeePercent { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeFixed { get; set; } = 0;

    public int CodeExpirationMinutes { get; set; } = 30;

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<DepositRequest> DepositRequests { get; set; } = new List<DepositRequest>();
}

public enum ProcessorType
{
    CryptoBot,
    Bybit,
    Manual,
    Webhook
}
