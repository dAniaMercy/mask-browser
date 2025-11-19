using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskAdmin.Models;

public class AuditLog
{
    [Key]
    public int Id { get; set; }

    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Entity { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? OldValues { get; set; } // JSON

    public string? NewValues { get; set; } // JSON

    public AuditLogLevel Level { get; set; } = AuditLogLevel.Info;

    public LogCategory Category { get; set; }

    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    [MaxLength(200)]
    public string UserAgent { get; set; } = string.Empty;

    public string? AdditionalData { get; set; } // JSON

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AuditLogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}

public enum LogCategory
{
    Authentication = 0,
    UserManagement = 1,
    ProfileManagement = 2,
    ServerManagement = 3,
    SubscriptionManagement = 4,
    PaymentManagement = 5,
    SystemSettings = 6,
    Security = 7,
    API = 8,
    Other = 9
}
