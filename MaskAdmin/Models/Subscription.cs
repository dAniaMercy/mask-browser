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

    public int? PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public virtual SubscriptionPlan? Plan { get; set; }

    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public int MaxProfiles { get; set; } = 1;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Price { get; set; } = 0;

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool AutoRenew { get; set; } = false;

    public DateTime? NextBillingDate { get; set; }

    public int? PaymentId { get; set; }

    [ForeignKey(nameof(PaymentId))]
    public virtual Payment? LastPayment { get; set; }

    [MaxLength(200)]
    public string? StripeSubscriptionId { get; set; }

    [MaxLength(200)]
    public string? StripeCustomerId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateTime? CancelledAt { get; set; }

    public DateTime? TrialEndsAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public enum SubscriptionTier
{
    Free = 0,
    Starter = 1,
    Pro = 2,
    Business = 3,
    Enterprise = 4
}

public enum BillingCycle
{
    Monthly = 0,
    Yearly = 1,
    Lifetime = 2
}

public enum SubscriptionStatus
{
    Active = 0,
    PastDue = 1,
    Cancelled = 2,
    Expired = 3,
    Trial = 4,
    Suspended = 5
}
