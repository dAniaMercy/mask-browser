namespace MaskBrowser.Server.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; } = false;

    // 2FA/TOTP
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }
    public string? TwoFactorRecoveryCodes { get; set; } // JSON array of recovery codes

    // Navigation properties
    public List<BrowserProfile> BrowserProfiles { get; set; } = new();
    public Subscription? Subscription { get; set; }
    public List<Payment> Payments { get; set; } = new();
}

