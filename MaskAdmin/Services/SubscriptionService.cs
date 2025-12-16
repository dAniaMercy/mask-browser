using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly ILogService _logService;

    public SubscriptionService(ApplicationDbContext context, ILogger<SubscriptionService> logger, ILogService logService)
    {
        _context = context;
        _logger = logger;
        _logService = logService;
    }

    public async Task<(List<Subscription> Subscriptions, int TotalCount)> GetSubscriptionsAsync(int page, int pageSize)
    {
        var query = _context.Subscriptions.Include(s => s.User);
        var total = await query.CountAsync();
        var subscriptions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (subscriptions, total);
    }

    public async Task<Subscription?> GetSubscriptionByIdAsync(int id)
    {
        return await _context.Subscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<bool> CreateSubscriptionAsync(Subscription subscription)
    {
        try
        {
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subscription");
            return false;
        }
    }

    public async Task<bool> UpdateSubscriptionAsync(int id, Subscription subscription)
    {
        try
        {
            var existing = await _context.Subscriptions.FindAsync(id);
            if (existing == null) return false;

            existing.Tier = subscription.Tier;
            existing.MaxProfiles = subscription.MaxProfiles;
            existing.Price = subscription.Price;
            existing.IsActive = subscription.IsActive;
            existing.EndDate = subscription.EndDate;
            existing.AutoRenew = subscription.AutoRenew;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription");
            return false;
        }
    }

    public async Task<bool> DeleteSubscriptionAsync(int id)
    {
        try
        {
            var subscription = await _context.Subscriptions.FindAsync(id);
            if (subscription == null) return false;

            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subscription");
            return false;
        }
    }

    public async Task<bool> AssignSubscriptionToUserAsync(int userId, SubscriptionTier tier, int maxProfiles, DateTime? endDate)
    {
        try
        {
            var existingSubscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

            if (existingSubscription != null)
            {
                existingSubscription.Tier = tier;
                existingSubscription.MaxProfiles = maxProfiles;
                existingSubscription.EndDate = endDate;
                existingSubscription.IsActive = true;
                existingSubscription.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var subscription = new Subscription
                {
                    UserId = userId,
                    Tier = tier,
                    MaxProfiles = maxProfiles,
                    EndDate = endDate,
                    IsActive = true
                };
                _context.Subscriptions.Add(subscription);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning subscription");
            return false;
        }
    }

    public async Task<SubscriptionStats> GetSubscriptionStatsAsync()
    {
        var subscriptions = await _context.Subscriptions.ToListAsync();

        return new SubscriptionStats
        {
            ByTier = subscriptions
                .GroupBy(s => s.Tier)
                .ToDictionary(g => g.Key, g => g.Count()),
            TotalActive = subscriptions.Count(s => s.IsActive),
            TotalExpired = subscriptions.Count(s => !s.IsActive || (s.EndDate.HasValue && s.EndDate < DateTime.UtcNow)),
            MonthlyRecurringRevenue = subscriptions.Where(s => s.IsActive).Sum(s => s.Price)
        };
    }

    // New monetization methods

    public async Task<Subscription?> GetUserSubscriptionAsync(int userId)
    {
        return await _context.Subscriptions
            .Include(s => s.Plan)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<SubscriptionPlan?> GetPlanByTierAsync(SubscriptionTier tier)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Tier == tier && p.IsActive);
    }

    public async Task<List<SubscriptionPlan>> GetAllActivePlansAsync()
    {
        return await _context.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }

    public async Task<bool> CanUserCreateProfileAsync(int userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        if (subscription == null || subscription.Plan == null)
            return false;

        var currentProfileCount = await GetUserProfileCountAsync(userId);
        return currentProfileCount < subscription.Plan.MaxProfiles;
    }

    public async Task<int> GetUserProfileCountAsync(int userId)
    {
        return await _context.BrowserProfiles
            .CountAsync(p => p.UserId == userId);
    }

    public async Task<bool> CheckFeatureAccessAsync(int userId, string featureName)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        if (subscription?.Plan == null)
            return false;

        return featureName switch
        {
            "CloudProfiles" => subscription.Plan.CloudProfilesEnabled,
            "TeamCollaboration" => subscription.Plan.TeamCollaborationEnabled,
            "PrioritySupport" => subscription.Plan.PrioritySupport,
            "AdvancedFingerprints" => subscription.Plan.AdvancedFingerprintsEnabled,
            "ApiAccess" => subscription.Plan.ApiAccessEnabled,
            "CustomBranding" => subscription.Plan.CustomBrandingEnabled,
            "DedicatedAccountManager" => subscription.Plan.DedicatedAccountManagerEnabled,
            _ => false
        };
    }

    public async Task<bool> UpgradeSubscriptionAsync(int userId, int planId, BillingCycle billingCycle, int? paymentId = null)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            var newPlan = await _context.SubscriptionPlans.FindAsync(planId);
            if (newPlan == null || !newPlan.IsActive)
                return false;

            var currentSubscription = await GetUserSubscriptionAsync(userId);

            var price = billingCycle == BillingCycle.Yearly ? newPlan.YearlyPrice : newPlan.MonthlyPrice;

            if (currentSubscription != null)
            {
                currentSubscription.IsActive = false;
                currentSubscription.EndDate = DateTime.UtcNow;
                currentSubscription.UpdatedAt = DateTime.UtcNow;
                _context.Subscriptions.Update(currentSubscription);
            }

            var newSubscription = new Subscription
            {
                UserId = userId,
                PlanId = planId,
                Tier = newPlan.Tier,
                BillingCycle = billingCycle,
                MaxProfiles = newPlan.MaxProfiles,
                Price = price,
                IsActive = true,
                Status = SubscriptionStatus.Active,
                StartDate = DateTime.UtcNow,
                AutoRenew = true,
                PaymentId = paymentId
            };

            if (billingCycle == BillingCycle.Yearly)
            {
                newSubscription.NextBillingDate = DateTime.UtcNow.AddYears(1);
                newSubscription.EndDate = DateTime.UtcNow.AddYears(1);
            }
            else if (billingCycle == BillingCycle.Monthly)
            {
                newSubscription.NextBillingDate = DateTime.UtcNow.AddMonths(1);
                newSubscription.EndDate = DateTime.UtcNow.AddMonths(1);
            }

            _context.Subscriptions.Add(newSubscription);
            await _context.SaveChangesAsync();

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Info,
                "Subscription Upgraded",
                $"User {user.Username} upgraded to {newPlan.Name} ({billingCycle})",
                userId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upgrading subscription");
            return false;
        }
    }

    public async Task<bool> DowngradeSubscriptionAsync(int userId, int planId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            var newPlan = await _context.SubscriptionPlans.FindAsync(planId);
            if (newPlan == null || !newPlan.IsActive)
                return false;

            var currentSubscription = await GetUserSubscriptionAsync(userId);
            if (currentSubscription == null)
                return false;

            currentSubscription.UpdatedAt = DateTime.UtcNow;

            if (newPlan.Tier == SubscriptionTier.Free)
            {
                currentSubscription.IsActive = false;
                currentSubscription.Status = SubscriptionStatus.Cancelled;
                currentSubscription.CancelledAt = DateTime.UtcNow;
                currentSubscription.AutoRenew = false;

                var freeSubscription = new Subscription
                {
                    UserId = userId,
                    PlanId = planId,
                    Tier = SubscriptionTier.Free,
                    MaxProfiles = newPlan.MaxProfiles,
                    Price = 0,
                    IsActive = true,
                    Status = SubscriptionStatus.Active,
                    StartDate = DateTime.UtcNow
                };

                _context.Subscriptions.Add(freeSubscription);
            }
            else
            {
                currentSubscription.AutoRenew = false;
            }

            _context.Subscriptions.Update(currentSubscription);
            await _context.SaveChangesAsync();

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Info,
                "Subscription Downgraded",
                $"User {user.Username} downgraded to {newPlan.Name}",
                userId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downgrading subscription");
            return false;
        }
    }

    public async Task<bool> CancelSubscriptionAsync(int userId, bool immediate = false)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            var subscription = await GetUserSubscriptionAsync(userId);
            if (subscription == null)
                return false;

            if (immediate)
            {
                subscription.IsActive = false;
                subscription.Status = SubscriptionStatus.Cancelled;
                subscription.CancelledAt = DateTime.UtcNow;
                subscription.EndDate = DateTime.UtcNow;

                var freePlan = await GetPlanByTierAsync(SubscriptionTier.Free);
                if (freePlan != null)
                {
                    var freeSubscription = new Subscription
                    {
                        UserId = userId,
                        PlanId = freePlan.Id,
                        Tier = SubscriptionTier.Free,
                        MaxProfiles = freePlan.MaxProfiles,
                        Price = 0,
                        IsActive = true,
                        Status = SubscriptionStatus.Active,
                        StartDate = DateTime.UtcNow
                    };

                    _context.Subscriptions.Add(freeSubscription);
                }
            }
            else
            {
                subscription.AutoRenew = false;
                subscription.CancelledAt = DateTime.UtcNow;
            }

            subscription.UpdatedAt = DateTime.UtcNow;
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Info,
                "Subscription Cancelled",
                $"User {user.Username} cancelled subscription (immediate: {immediate})",
                userId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            return false;
        }
    }

    public async Task<bool> RenewSubscriptionAsync(int subscriptionId)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.Plan)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId);

            if (subscription == null || subscription.Plan == null)
                return false;

            if (!subscription.AutoRenew)
                return false;

            if (subscription.BillingCycle == BillingCycle.Yearly)
            {
                subscription.NextBillingDate = subscription.NextBillingDate?.AddYears(1);
                subscription.EndDate = subscription.EndDate?.AddYears(1);
            }
            else if (subscription.BillingCycle == BillingCycle.Monthly)
            {
                subscription.NextBillingDate = subscription.NextBillingDate?.AddMonths(1);
                subscription.EndDate = subscription.EndDate?.AddMonths(1);
            }

            subscription.UpdatedAt = DateTime.UtcNow;
            _context.Subscriptions.Update(subscription);
            await _context.SaveChangesAsync();

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Info,
                "Subscription Renewed",
                $"Subscription {subscriptionId} renewed for {subscription.User.Username}",
                subscription.UserId
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing subscription");
            return false;
        }
    }

    public async Task ProcessExpiredSubscriptionsAsync()
    {
        var expiredSubscriptions = await _context.Subscriptions
            .Include(s => s.User)
            .Where(s => s.IsActive
                && s.EndDate.HasValue
                && s.EndDate.Value < DateTime.UtcNow
                && !s.AutoRenew)
            .ToListAsync();

        foreach (var subscription in expiredSubscriptions)
        {
            subscription.IsActive = false;
            subscription.Status = SubscriptionStatus.Expired;
            subscription.UpdatedAt = DateTime.UtcNow;

            var freePlan = await GetPlanByTierAsync(SubscriptionTier.Free);
            if (freePlan != null)
            {
                var freeSubscription = new Subscription
                {
                    UserId = subscription.UserId,
                    PlanId = freePlan.Id,
                    Tier = SubscriptionTier.Free,
                    MaxProfiles = freePlan.MaxProfiles,
                    Price = 0,
                    IsActive = true,
                    Status = SubscriptionStatus.Active,
                    StartDate = DateTime.UtcNow
                };

                _context.Subscriptions.Add(freeSubscription);
            }

            await _logService.LogAsync(
                LogCategory.SubscriptionManagement,
                LogLevel.Warning,
                "Subscription Expired",
                $"Subscription expired for {subscription.User.Username}",
                subscription.UserId
            );
        }

        if (expiredSubscriptions.Any())
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task ProcessAutoRenewalsAsync()
    {
        var renewalsDue = await _context.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.IsActive
                && s.AutoRenew
                && s.NextBillingDate.HasValue
                && s.NextBillingDate.Value <= DateTime.UtcNow.AddDays(1))
            .ToListAsync();

        foreach (var subscription in renewalsDue)
        {
            await RenewSubscriptionAsync(subscription.Id);
        }
    }
}
