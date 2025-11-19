using MaskAdmin.Models;
using MaskAdmin.ViewModels;

namespace MaskAdmin.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetDashboardDataAsync();
    Task<DashboardStats> GetStatsAsync();
    Task<List<ChartDataPoint>> GetProfilesChartDataAsync(int days = 7);
    Task<List<ChartDataPoint>> GetUsersChartDataAsync(int days = 7);
    Task<List<ChartDataPoint>> GetRevenueChartDataAsync(int days = 30);
}

public interface IUserService
{
    Task<(List<User> Users, int TotalCount)> GetUsersAsync(int page, int pageSize, string? search, string? status, string? sort);
    Task<User?> GetUserByIdAsync(int id);
    Task<bool> UpdateUserAsync(int id, User user);
    Task<bool> AdjustBalanceAsync(int id, decimal amount, string reason);
    Task<bool> BanUserAsync(int id, string reason);
    Task<bool> UnbanUserAsync(int id);
    Task<bool> FreezeUserAsync(int id, string reason);
    Task<bool> DeleteUserAsync(int id);
    Task<bool> ResetPasswordAsync(int id, string newPassword);
    Task<UserStats> GetUserStatsAsync(int id);
    Task<List<AuditLog>> GetUserLogsAsync(int id, int page, int pageSize);
}

public interface ISubscriptionService
{
    Task<(List<Subscription> Subscriptions, int TotalCount)> GetSubscriptionsAsync(int page, int pageSize);
    Task<Subscription?> GetSubscriptionByIdAsync(int id);
    Task<bool> CreateSubscriptionAsync(Subscription subscription);
    Task<bool> UpdateSubscriptionAsync(int id, Subscription subscription);
    Task<bool> DeleteSubscriptionAsync(int id);
    Task<bool> AssignSubscriptionToUserAsync(int userId, SubscriptionTier tier, int maxProfiles, DateTime? endDate);
    Task<SubscriptionStats> GetSubscriptionStatsAsync();
}

public interface IServerService
{
    Task<(List<ServerNode> Servers, int TotalCount)> GetServersAsync(int page, int pageSize);
    Task<ServerNode?> GetServerByIdAsync(int id);
    Task<bool> RegisterServerAsync(ServerNode server);
    Task<bool> UpdateServerAsync(int id, ServerNode server);
    Task<bool> DeleteServerAsync(int id);
    Task<bool> RestartServerAsync(int id);
    Task<List<BrowserProfile>> GetServerContainersAsync(int id);
    Task<ServerMetrics> GetServerMetricsAsync(int id);
    Task<List<AuditLog>> GetServerLogsAsync(int id, int page, int pageSize);
}

public interface IProfileService
{
    Task<(List<BrowserProfile> Profiles, int TotalCount)> GetProfilesAsync(int page, int pageSize, int? userId, ProfileStatus? status, int? serverId);
    Task<BrowserProfile?> GetProfileByIdAsync(int id);
    Task<bool> StartProfileAsync(int id);
    Task<bool> StopProfileAsync(int id);
    Task<bool> DeleteProfileAsync(int id);
    Task<ProfileStats> GetProfileStatsAsync(int id);
    Task<List<AuditLog>> GetProfileLogsAsync(int id, int page, int pageSize);
}

public interface IPaymentService
{
    Task<(List<Payment> Payments, int TotalCount)> GetPaymentsAsync(int page, int pageSize, PaymentStatus? status, PaymentProvider? provider, int? userId);
    Task<Payment?> GetPaymentByIdAsync(int id);
    Task<PaymentStats> GetPaymentStatsAsync();
    Task<byte[]> ExportPaymentsAsync(string format);
}

public interface ILogService
{
    Task<(List<AuditLog> Logs, int TotalCount)> GetLogsAsync(int page, int pageSize, LogCategory? category, AuditLogLevel? level, DateTime? from, DateTime? to, string? search);
    Task<byte[]> ExportLogsAsync(string format, LogCategory? category, DateTime? from, DateTime? to);
}

public interface ISettingsService
{
    Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category);
    Task<bool> UpdateSettingAsync(string key, string value);
    Task<string?> GetSettingAsync(string key);
}

public interface IExportService
{
    Task<byte[]> ExportToCsvAsync<T>(List<T> data, string[] headers);
    Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName, string[] headers);
}

public interface INotificationService
{
    Task SendNotificationAsync(string userId, string message, string type);
    Task BroadcastNotificationAsync(string message, string type);
}

// Stats DTOs
public class UserStats
{
    public int TotalProfiles { get; set; }
    public int ActiveProfiles { get; set; }
    public decimal TotalSpent { get; set; }
    public int TotalLogins { get; set; }
    public DateTime? LastLogin { get; set; }
    public List<Payment> RecentPayments { get; set; } = new();
}

public class SubscriptionStats
{
    public Dictionary<SubscriptionTier, int> ByTier { get; set; } = new();
    public int TotalActive { get; set; }
    public int TotalExpired { get; set; }
    public decimal MonthlyRecurringRevenue { get; set; }
}

public class ServerMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
    public long NetworkIn { get; set; }
    public long NetworkOut { get; set; }
    public int ActiveContainers { get; set; }
    public DateTime LastHealthCheck { get; set; }
}

public class ProfileStats
{
    public int TotalRunTime { get; set; }
    public int StartCount { get; set; }
    public DateTime? LastStarted { get; set; }
    public string ServerLocation { get; set; } = string.Empty;
}

public class PaymentStats
{
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public int TotalPayments { get; set; }
    public int SuccessfulPayments { get; set; }
    public int FailedPayments { get; set; }
    public Dictionary<PaymentProvider, decimal> ByProvider { get; set; } = new();
}
