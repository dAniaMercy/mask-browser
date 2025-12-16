using MaskAdmin.Models;

namespace MaskAdmin.Services;

public interface ISubscriptionService
{
    // Existing methods
    Task<(List<Subscription> Subscriptions, int TotalCount)> GetSubscriptionsAsync(int page, int pageSize);
    Task<Subscription?> GetSubscriptionByIdAsync(int id);
    Task<bool> CreateSubscriptionAsync(Subscription subscription);
    Task<bool> UpdateSubscriptionAsync(int id, Subscription subscription);
    Task<bool> DeleteSubscriptionAsync(int id);
    Task<bool> AssignSubscriptionToUserAsync(int userId, SubscriptionTier tier, int maxProfiles, DateTime? endDate);
    Task<SubscriptionStats> GetSubscriptionStatsAsync();

    // New monetization methods
    Task<Subscription?> GetUserSubscriptionAsync(int userId);
    Task<SubscriptionPlan?> GetPlanByTierAsync(SubscriptionTier tier);
    Task<List<SubscriptionPlan>> GetAllActivePlansAsync();
    Task<bool> CanUserCreateProfileAsync(int userId);
    Task<bool> UpgradeSubscriptionAsync(int userId, int planId, BillingCycle billingCycle, int? paymentId = null);
    Task<bool> DowngradeSubscriptionAsync(int userId, int planId);
    Task<bool> CancelSubscriptionAsync(int userId, bool immediate = false);
    Task<bool> RenewSubscriptionAsync(int subscriptionId);
    Task<int> GetUserProfileCountAsync(int userId);
    Task<bool> CheckFeatureAccessAsync(int userId, string featureName);
    Task ProcessExpiredSubscriptionsAsync();
    Task ProcessAutoRenewalsAsync();
}
