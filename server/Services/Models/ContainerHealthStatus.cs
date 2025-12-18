namespace MaskBrowser.Server.Services.Models;

public class ContainerHealthStatus
{
    public string ContainerId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = "unknown";
    public DateTime? StartedAt { get; set; }
    public TimeSpan Uptime { get; set; }
    public int Port { get; set; }
    public long? ExitCode { get; set; }
}
