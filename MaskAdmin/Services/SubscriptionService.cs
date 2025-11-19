using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(ApplicationDbContext context, ILogger<SubscriptionService> logger)
    {
        _context = context;
        _logger = logger;
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
}
