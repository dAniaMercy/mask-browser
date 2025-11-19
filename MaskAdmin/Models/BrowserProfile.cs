using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaskAdmin.Models;

public class BrowserProfile
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string ContainerId { get; set; } = string.Empty;

    public int? ServerNodeId { get; set; }

    [ForeignKey(nameof(ServerNodeId))]
    public virtual ServerNode? ServerNode { get; set; }

    public string ServerNodeIp { get; set; } = string.Empty;

    public int Port { get; set; } = 0;

    public ProfileStatus Status { get; set; } = ProfileStatus.Stopped;

    // Browser Configuration (stored as JSON in Config property)
    public string UserAgent { get; set; } = string.Empty;
    public string ScreenResolution { get; set; } = "1920x1080";
    public string Timezone { get; set; } = "UTC";
    public string Language { get; set; } = "en-US";
    public bool WebRTC { get; set; } = false;
    public bool Canvas { get; set; } = false;
    public bool WebGL { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastStoppedAt { get; set; }

    public int TotalRunTime { get; set; } = 0; // in minutes

    public int StartCount { get; set; } = 0;
}

public enum ProfileStatus
{
    Stopped = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Error = 4
}
