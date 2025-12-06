using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Models;
using MaskBrowser.Server.Services;
using System.ComponentModel.DataAnnotations;

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
    try
    {
            _logger.LogInformation("Registration attempt: Email={Email}, Username={Username}", 
                request?.Email ?? "null", request?.Username ?? "null");

            // Проверка ModelState
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(new { message = "Invalid request data", errors = ModelState });
            }

            // Проверка на null
            if (request == null)
            {
                _logger.LogWarning("Register request is null");
                return BadRequest(new { message = "Request body is required" });
            }

            // Валидация полей
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new { message = "Username is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Password is required" });
            }

            if (request.Password.Length < 6)
            {
                return BadRequest(new { message = "Password must be at least 6 characters long" });
            }

        var user = await _authService.RegisterAsync(request.Username, request.Email, request.Password);
        if (user == null)
        {
                _logger.LogWarning("Registration failed: User already exists - Email={Email}, Username={Username}", 
                    request.Email, request.Username);
            return BadRequest(new { message = "User already exists" });
        }

        var token = _authService.GenerateJwtToken(user);
            _logger.LogInformation("User registered successfully: Id={UserId}, Username={Username}, Email={Email}", 
                user.Id, user.Username, user.Email);

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
    catch (Exception ex)
    {
            _logger.LogError(ex, "Registration failed for Email={Email}, Username={Username}", 
                request?.Email ?? "unknown", request?.Username ?? "unknown");
        return StatusCode(500, new { message = "Server error during registration", error = ex.Message });
    }
}


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt: Email={Email}", request?.Email ?? "null");

        if (!ModelState.IsValid || request == null)
        {
            _logger.LogWarning("Invalid login request");
            return BadRequest(new { message = "Invalid request data", errors = ModelState });
        }

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
    public IActionResult GetMe()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var isAdmin = User.IsInRole("Admin");
        var twoFactorEnabled = User.FindFirst("TwoFactorEnabled")?.Value == "True";
        
        return Ok(new 
        { 
            userId,
            username,
            email,
            isAdmin,
            twoFactorEnabled
        });
    }
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
    [MaxLength(50, ErrorMessage = "Username must not exceed 50 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    public string? TwoFactorCode { get; set; }
}

public class DisableTwoFactorRequest
{
    public string Password { get; set; } = string.Empty;
}

