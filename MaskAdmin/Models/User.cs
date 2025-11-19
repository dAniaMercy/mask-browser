using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskAdmin.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Balance { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public bool IsBanned { get; set; } = false;

    public bool IsFrozen { get; set; } = false;

    public bool IsAdmin { get; set; } = false;

    public bool TwoFactorEnabled { get; set; } = false;

    public string? TwoFactorSecret { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginIp { get; set; }

    // Navigation properties
    public virtual Subscription? Subscription { get; set; }
    public virtual ICollection<BrowserProfile> BrowserProfiles { get; set; } = new List<BrowserProfile>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
