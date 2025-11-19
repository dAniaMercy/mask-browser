namespace MaskAdmin.ViewModels;

public class DashboardViewModel
{
    public DashboardStats Stats { get; set; } = new();
    public List<RecentActivity> RecentActivities { get; set; } = new();
    public List<ServerNodeInfo> ServerNodes { get; set; } = new();
    public List<ChartDataPoint> ProfilesChartData { get; set; } = new();
    public List<ChartDataPoint> UsersChartData { get; set; } = new();
    public List<ChartDataPoint> RevenueChartData { get; set; } = new();
    public List<TopUser> TopUsers { get; set; } = new();
}

public class DashboardStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalProfiles { get; set; }
    public int ActiveProfiles { get; set; }
    public int TotalServers { get; set; }
    public int HealthyServers { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int PendingPayments { get; set; }
    public int TotalSubscriptions { get; set; }
}

public class RecentActivity
{
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ServerNodeInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int ActiveContainers { get; set; }
    public int MaxContainers { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double LoadPercentage => MaxContainers > 0 ? (double)ActiveContainers / MaxContainers * 100 : 0;
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Date { get; set; }
}

public class TopUser
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ProfileCount { get; set; }
    public int ActiveProfileCount { get; set; }
    public decimal TotalSpent { get; set; }
    public string SubscriptionTier { get; set; } = string.Empty;
}
