using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskAdmin.Models;

public class Subscription
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public int MaxProfiles { get; set; } = 1;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Price { get; set; } = 0;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool AutoRenew { get; set; } = false;

    public DateTime? NextBillingDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public enum SubscriptionTier
{
    Free = 0,
    Basic = 1,
    Pro = 2,
    Enterprise = 3,
    Custom = 4
}
