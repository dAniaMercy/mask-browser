using System.ComponentModel.DataAnnotations;

namespace MaskAdmin.Models;

public class ServerNode
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public int MaxContainers { get; set; } = 1000;

    public int ActiveContainers { get; set; } = 0;

    public bool IsHealthy { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public double CpuUsage { get; set; } = 0;

    public double MemoryUsage { get; set; } = 0;

    public double DiskUsage { get; set; } = 0;

    public long NetworkIn { get; set; } = 0; // bytes

    public long NetworkOut { get; set; } = 0; // bytes

    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<BrowserProfile> BrowserProfiles { get; set; } = new List<BrowserProfile>();
}
