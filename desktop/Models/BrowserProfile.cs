using System;

namespace MaskBrowser.Desktop.Models
{
    public class BrowserProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContainerId { get; set; } = string.Empty;
        public string ServerNodeIp { get; set; } = string.Empty;
        public int Port { get; set; }
        public BrowserConfig Config { get; set; } = new();
        public string Status { get; set; } = "Stopped";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastStartedAt { get; set; }
    }

    public class BrowserConfig
    {
        public string UserAgent { get; set; } = string.Empty;
        public string ScreenResolution { get; set; } = "1920x1080";
        public string Timezone { get; set; } = "UTC";
        public string Language { get; set; } = "en-US";
        public bool WebRTC { get; set; } = false;
        public bool Canvas { get; set; } = false;
        public bool WebGL { get; set; } = false;
    }
}

