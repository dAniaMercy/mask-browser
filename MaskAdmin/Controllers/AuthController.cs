using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MaskAdmin.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace MaskAdmin.Controllers;

[AllowAnonymous]
public class AuthController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext context,
        ILogger<AuthController> logger)
    {
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
            _logger.LogInformation("Login attempt for username: {Username}", username);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewData["Error"] = "Username and password are required";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Find user by username or email
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Username == username || u.Email == username);

            if (user == null)
            {
                _logger.LogWarning("User not found: {Username}", username);
                ViewData["Error"] = "Invalid username or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            _logger.LogInformation("User found: {Username}, ID: {Id}, IsActive: {IsActive}, IsAdmin: {IsAdmin}",
                user.Username, user.Id, user.IsActive, user.IsAdmin);

            // Verify password
            bool passwordValid = false;
            try
            {
                passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password verification error for user: {Username}", username);
                ViewData["Error"] = "Password verification error";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            if (!passwordValid)
            {
                _logger.LogWarning("Invalid password for user: {Username}", username);
                ViewData["Error"] = "Invalid username or password";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login attempt for inactive user: {Username}", username);
                ViewData["Error"] = "Account is disabled";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Check if user is banned
            if (user.IsBanned)
            {
                _logger.LogWarning("Login attempt for banned user: {Username}", username);
                ViewData["Error"] = "Account is banned";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Check if user is frozen
            if (user.IsFrozen)
            {
                _logger.LogWarning("Login attempt for frozen user: {Username}", username);
                ViewData["Error"] = "Account is temporarily frozen";
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            // Update last login
            try
            {
                user.LastLoginAt = DateTime.UtcNow;
                user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                _context.Users.Attach(user);
                _context.Entry(user).Property(u => u.LastLoginAt).IsModified = true;
                _context.Entry(user).Property(u => u.LastLoginIp).IsModified = true;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update last login for user: {Username}", username);
                // Continue anyway, not critical
            }

            // Create claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
                new Claim("IsAdmin", user.IsAdmin.ToString()),
                new Claim("UserId", user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                AllowRefresh = true
            };

            // Sign in the user
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                authProperties);

            _logger.LogInformation("User {Username} (ID: {UserId}) logged in successfully", user.Username, user.Id);

            // Redirect
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username: {Username}", username);
            ViewData["Error"] = "An error occurred during login. Please try again.";
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var username = User.Identity?.Name ?? "Unknown";
            _logger.LogInformation("User {Username} logging out", username);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return RedirectToAction("Login");
        }
    }
}
