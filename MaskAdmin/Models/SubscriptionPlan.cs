using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskAdmin.Models;

public class SubscriptionPlan
{
    [Key]
    public int Id { get; set; }

    public SubscriptionTier Tier { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal MonthlyPrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal YearlyPrice { get; set; }

    public int MaxProfiles { get; set; }

    public int MaxTeamMembers { get; set; } = 1;

    public bool CloudProfilesEnabled { get; set; } = false;

    public bool TeamCollaborationEnabled { get; set; } = false;

    public bool PrioritySupport { get; set; } = false;

    public bool AdvancedFingerprintsEnabled { get; set; } = false;

    public bool ApiAccessEnabled { get; set; } = false;

    public int ApiRequestsPerDay { get; set; } = 0;

    public bool CustomBrandingEnabled { get; set; } = false;

    public bool DedicatedAccountManagerEnabled { get; set; } = false;

    public int StorageGB { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public bool IsPopular { get; set; } = false;

    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

public class PlanFeature
{
    [Key]
    public int Id { get; set; }

    public int PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public virtual SubscriptionPlan Plan { get; set; } = null!;

    [MaxLength(200)]
    public string FeatureName { get; set; } = string.Empty;

    public bool IsIncluded { get; set; } = true;

    public string? FeatureValue { get; set; }

    public int SortOrder { get; set; } = 0;
}
