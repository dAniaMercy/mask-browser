using MaskAdmin.Data;
using MaskAdmin.Models;
using MaskAdmin.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace MaskAdmin.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<DashboardService> _logger;
    private const string CACHE_KEY_PREFIX = "dashboard_";
    private const int CACHE_EXPIRATION_MINUTES = 5;

    public DashboardService(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync()
    {
        try
        {
            var viewModel = new DashboardViewModel
            {
                Stats = await GetStatsAsync(),
                ServerNodes = await GetServerNodesInfoAsync(),
                ProfilesChartData = await GetProfilesChartDataAsync(7),
                UsersChartData = await GetUsersChartDataAsync(7),
                RevenueChartData = await GetRevenueChartDataAsync(30),
                TopUsers = await GetTopUsersAsync(5),
                RecentActivities = await GetRecentActivitiesAsync(10)
            };

            return viewModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard data");
            throw;
        }
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}stats";
        var cachedData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<DashboardStats>(cachedData) ?? new DashboardStats();
        }

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var stats = new DashboardStats
        {
            TotalUsers = await _context.Users.CountAsync(),
            ActiveUsers = await _context.Users.CountAsync(u => u.IsActive),
            TotalProfiles = await _context.BrowserProfiles.CountAsync(),
            ActiveProfiles = await _context.BrowserProfiles.CountAsync(p => p.Status == ProfileStatus.Running),
            TotalServers = await _context.ServerNodes.CountAsync(),
            HealthyServers = await _context.ServerNodes.CountAsync(s => s.IsHealthy && s.IsEnabled),
            TotalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount),
            MonthlyRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= monthStart)
                .SumAsync(p => p.Amount),
            PendingPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending),
            TotalSubscriptions = await _context.Subscriptions.CountAsync(s => s.IsActive)
        };

        // Cache for 5 minutes
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), options);

        return stats;
    }

    public async Task<List<ChartDataPoint>> GetProfilesChartDataAsync(int days = 7)
    {
        var startDate = DateTime.UtcNow.AddDays(-days).Date;
        var data = await _context.BrowserProfiles
            .Where(p => p.CreatedAt >= startDate)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Label = g.Key.ToString("dd MMM"),
                Value = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // Fill missing days with zeros
        var result = new List<ChartDataPoint>();
        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var existing = data.FirstOrDefault(d => d.Date == date);
            result.Add(existing ?? new ChartDataPoint
            {
                Date = date,
                Label = date.ToString("dd MMM"),
                Value = 0
            });
        }

        return result;
    }

    public async Task<List<ChartDataPoint>> GetUsersChartDataAsync(int days = 7)
    {
        var startDate = DateTime.UtcNow.AddDays(-days).Date;
        var data = await _context.Users
            .Where(u => u.CreatedAt >= startDate)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Label = g.Key.ToString("dd MMM"),
                Value = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // Fill missing days
        var result = new List<ChartDataPoint>();
        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var existing = data.FirstOrDefault(d => d.Date == date);
            result.Add(existing ?? new ChartDataPoint
            {
                Date = date,
                Label = date.ToString("dd MMM"),
                Value = 0
            });
        }

        return result;
    }

    public async Task<List<ChartDataPoint>> GetRevenueChartDataAsync(int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days).Date;
        var data = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CompletedAt >= startDate)
            .GroupBy(p => p.CompletedAt!.Value.Date)
            .Select(g => new ChartDataPoint
            {
                Date = g.Key,
                Label = g.Key.ToString("dd MMM"),
                Value = (double)g.Sum(p => p.Amount)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // Fill missing days
        var result = new List<ChartDataPoint>();
        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var existing = data.FirstOrDefault(d => d.Date == date);
            result.Add(existing ?? new ChartDataPoint
            {
                Date = date,
                Label = date.ToString("dd MMM"),
                Value = 0
            });
        }

        return result;
    }

    private async Task<List<ServerNodeInfo>> GetServerNodesInfoAsync()
    {
        return await _context.ServerNodes
            .Select(s => new ServerNodeInfo
            {
                Id = s.Id,
                Name = s.Name,
                IpAddress = s.IpAddress,
                IsHealthy = s.IsHealthy,
                ActiveContainers = s.ActiveContainers,
                MaxContainers = s.MaxContainers,
                CpuUsage = s.CpuUsage,
                MemoryUsage = s.MemoryUsage
            })
            .ToListAsync();
    }

    private async Task<List<TopUser>> GetTopUsersAsync(int count)
    {
        return await _context.Users
            .Include(u => u.BrowserProfiles)
            .Include(u => u.Subscription)
            .Include(u => u.Payments)
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.BrowserProfiles.Count)
            .Take(count)
            .Select(u => new TopUser
            {
                UserId = u.Id,
                Username = u.Username,
                Email = u.Email,
                ProfileCount = u.BrowserProfiles.Count,
                ActiveProfileCount = u.BrowserProfiles.Count(p => p.Status == ProfileStatus.Running),
                TotalSpent = u.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
                SubscriptionTier = u.Subscription != null ? u.Subscription.Tier.ToString() : "Free"
            })
            .ToListAsync();
    }

    private async Task<List<RecentActivity>> GetRecentActivitiesAsync(int count)
    {
        return await _context.AuditLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .Select(l => new RecentActivity
            {
                Action = l.Action,
                User = l.User != null ? l.User.Username : "System",
                Timestamp = l.CreatedAt,
                Details = l.Entity + (l.EntityId.HasValue ? $" #{l.EntityId}" : ""),
                Category = l.Category.ToString()
            })
            .ToListAsync();
    }
}
