using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class LoginResult
{
    public bool Success { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public User? User { get; set; }
}

public class TwoFactorSetupResult
{
    public bool Success { get; set; }
    public string? Secret { get; set; }
    public string? QrCode { get; set; }
    public string[]? RecoveryCodes { get; set; }
}

