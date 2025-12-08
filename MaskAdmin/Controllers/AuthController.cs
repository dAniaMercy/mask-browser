using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MaskAdmin.Services;
using MaskAdmin.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace MaskAdmin.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewData["Error"] = "Username and password are required";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username || u.Email == username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", username);
                ViewData["Error"] = "Invalid username or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!user.IsActive)
            {
                ViewData["Error"] = "Account is disabled";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _context.SaveChangesAsync();

            // Create JWT token
            var token = GenerateJwtToken(user);

            // Set cookie with token
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddHours(8)
            };

            Response.Cookies.Append("auth_token", token, cookieOptions);

            _logger.LogInformation("User {Username} logged in successfully", user.Username);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            ViewData["Error"] = "An error occurred during login. Please try again.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        Response.Cookies.Delete("auth_token");
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }

    private string GenerateJwtToken(Models.User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("Role", user.IsAdmin ? "Admin" : "User")
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
