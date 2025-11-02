namespace MaskBrowser.Server.Models;

public class ServerNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int MaxContainers { get; set; } = 1000;
    public int ActiveContainers { get; set; } = 0;
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
    public bool IsHealthy { get; set; } = true;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

