using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Models;
using MaskBrowser.Server.Services;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(request.Username, request.Email, request.Password);
        if (user == null)
        {
            return BadRequest(new { message = "User already exists" });
        }

        var token = _authService.GenerateJwtToken(user);
        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.Username,
                user.Email
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password, request.TwoFactorCode);

        if (!result.Success)
        {
            if (result.RequiresTwoFactor)
            {
                return StatusCode(426, new { 
                    message = "Two-factor authentication required",
                    requires2FA = true 
                });
            }
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (result.User == null)
        {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var token = _authService.GenerateJwtToken(result.User);
        return Ok(new
        {
            token,
            user = new
            {
                result.User.Id,
                result.User.Username,
                result.User.Email,
                result.User.IsAdmin,
                result.User.TwoFactorEnabled
            }
        });
    }

    [HttpPost("two-factor/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _authService.EnableTwoFactorAsync(userId);

        if (!result.Success)
        {
            return BadRequest(new { message = "Failed to enable two-factor authentication" });
        }

        return Ok(new
        {
            secret = result.Secret,
            qrCode = result.QrCode,
            recoveryCodes = result.RecoveryCodes
        });
    }

    [HttpPost("two-factor/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _authService.DisableTwoFactorAsync(userId, request.Password);

        if (!result)
        {
            return BadRequest(new { message = "Invalid password" });
        }

        return Ok(new { message = "Two-factor authentication disabled" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        // Return user info
        return Ok(new { userId });
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? TwoFactorCode { get; set; }
}

public class DisableTwoFactorRequest
{
    public string Password { get; set; } = string.Empty;
}

