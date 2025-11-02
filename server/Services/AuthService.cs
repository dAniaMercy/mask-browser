using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class AuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly RsaKeyService _rsaKeyService;
    private readonly TotpService _totpService;

    public AuthService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        RsaKeyService rsaKeyService,
        TotpService totpService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _rsaKeyService = rsaKeyService;
        _totpService = totpService;
    }

    public async Task<User?> RegisterAsync(string username, string email, string password)
    {
        if (await _context.Users.AnyAsync(u => u.Email == email || u.Username == username))
        {
            return null;
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create free subscription
        var subscription = new Subscription
        {
            UserId = user.Id,
            Tier = SubscriptionTier.Free,
            MaxProfiles = 1,
            StartDate = DateTime.UtcNow
        };
        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {Username}", username);
        return user;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string? twoFactorCode = null)
    {
        var user = await _context.Users
            .Include(u => u.Subscription)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return new LoginResult { Success = false, RequiresTwoFactor = false };
        }

        if (!user.IsActive)
        {
            return new LoginResult { Success = false, RequiresTwoFactor = false };
        }

        // Check if 2FA is enabled
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrEmpty(twoFactorCode))
            {
                return new LoginResult 
                { 
                    Success = false, 
                    RequiresTwoFactor = true,
                    User = user 
                };
            }

            // Validate TOTP code
            bool isValid = false;
            if (!string.IsNullOrEmpty(user.TwoFactorSecret))
            {
                isValid = _totpService.ValidateTotp(user.TwoFactorSecret, twoFactorCode);
                
                // If TOTP failed, try recovery code
                if (!isValid && !string.IsNullOrEmpty(user.TwoFactorRecoveryCodes))
                {
                    var codes = System.Text.Json.JsonSerializer.Deserialize<string[]>(user.TwoFactorRecoveryCodes);
                    if (codes != null && codes.Contains(twoFactorCode))
                    {
                        isValid = true;
                        // Remove used recovery code
                        var updatedCodes = codes.Where(c => c != twoFactorCode).ToArray();
                        user.TwoFactorRecoveryCodes = System.Text.Json.JsonSerializer.Serialize(updatedCodes);
                    }
                }
            }

            if (!isValid)
            {
                return new LoginResult { Success = false, RequiresTwoFactor = true };
            }
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new LoginResult { Success = true, User = user };
    }

    public async Task<TwoFactorSetupResult> EnableTwoFactorAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return new TwoFactorSetupResult { Success = false };
        }

        var secret = _totpService.GenerateSecret();
        var qrCode = _totpService.GenerateQrCode(user.Email, secret);
        var recoveryCodes = _totpService.GenerateRecoveryCodes();

        user.TwoFactorSecret = secret;
        user.TwoFactorRecoveryCodes = System.Text.Json.JsonSerializer.Serialize(recoveryCodes);
        user.TwoFactorEnabled = true;

        await _context.SaveChangesAsync();

        return new TwoFactorSetupResult
        {
            Success = true,
            Secret = secret,
            QrCode = qrCode,
            RecoveryCodes = recoveryCodes
        };
    }

    public async Task<bool> DisableTwoFactorAsync(int userId, string password)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return false;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorRecoveryCodes = null;

        await _context.SaveChangesAsync();
        return true;
    }

    public string GenerateJwtToken(User user)
    {
        var signingKey = _rsaKeyService.GetPrivateKey();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
                new Claim("TwoFactorEnabled", user.TwoFactorEnabled.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(
                int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "15")
            ),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.RsaSha256
            )
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Guid.NewGuid().ToString();
    }
}

