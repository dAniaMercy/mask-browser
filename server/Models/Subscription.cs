namespace MaskBrowser.Server.Models;

public class Subscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
    public int MaxProfiles { get; set; } = 1;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public User User { get; set; } = null!;
}

public enum SubscriptionTier
{
    Free = 0,
    Basic = 1,
    Pro = 2,
    Enterprise = 3
}

