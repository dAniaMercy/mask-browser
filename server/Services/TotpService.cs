using OtpNet;
using QRCoder;
using System.Text;

namespace MaskBrowser.Server.Services;

public class TotpService
{
    private readonly ILogger<TotpService> _logger;

    public TotpService(ILogger<TotpService> logger)
    {
        _logger = logger;
    }

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160 bits
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCode(string email, string secret, string issuer = "MASK BROWSER")
    {
        var otpAuthUrl = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
        
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpAuthUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(20);
        
        return Convert.ToBase64String(qrCodeBytes);
    }

    public bool ValidateTotp(string secret, string code)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);
            
            var window = new VerificationWindow(previous: 1, future: 1);
            return totp.VerifyTotp(code, out _, window);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating TOTP code");
            return false;
        }
    }

    public string[] GenerateRecoveryCodes(int count = 10)
    {
        var codes = new string[count];
        var random = new Random();
        
        for (int i = 0; i < count; i++)
        {
            var code = new StringBuilder();
            for (int j = 0; j < 8; j++)
            {
                code.Append(random.Next(0, 10));
            }
            codes[i] = code.ToString();
        }
        
        return codes;
    }

    public bool ValidateRecoveryCode(string? storedRecoveryCodes, string code)
    {
        if (string.IsNullOrEmpty(storedRecoveryCodes))
        {
            return false;
        }

        try
        {
            var codes = System.Text.Json.JsonSerializer.Deserialize<string[]>(storedRecoveryCodes);
            if (codes == null || !codes.Contains(code))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}

