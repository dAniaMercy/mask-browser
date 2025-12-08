using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Models;
using MaskBrowser.Server.Infrastructure;
using System.Security.Claims;
using BCrypt.Net;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserController> _logger;

    public UserController(ApplicationDbContext context, ILogger<UserController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    /// <summary>
    /// Получить полную информацию о текущем пользователе
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var profiles = await _context.BrowserProfiles
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            var deposits = await _context.DepositRequests
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                balance = user.Balance,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
                isActive = user.IsActive,
                isAdmin = user.IsAdmin,
                twoFactorEnabled = user.TwoFactorEnabled,
                subscription = user.Subscription != null ? new
                {
                    tier = user.Subscription.Tier.ToString(),
                    maxProfiles = user.Subscription.MaxProfiles,
                    startDate = user.Subscription.StartDate,
                    endDate = user.Subscription.EndDate,
                    isActive = user.Subscription.IsActive
                } : null,
                stats = new
                {
                    totalProfiles = profiles.Count,
                    activeProfiles = profiles.Count(p => p.Status == ProfileStatus.Running),
                    totalPayments = payments.Count,
                    totalDeposits = deposits.Count,
                    completedDeposits = deposits.Count(d => d.Status == "completed")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "Failed to get user information" });
        }
    }

    /// <summary>
    /// Получить статистику пользователя
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var userId = GetCurrentUserId();

            var profiles = await _context.BrowserProfiles
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .ToListAsync();

            var deposits = await _context.DepositRequests
                .Where(d => d.UserId == userId)
                .ToListAsync();

            var totalSpent = payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .Sum(p => p.Amount);

            var totalDeposited = deposits
                .Where(d => d.Status == "completed")
                .Sum(d => d.ActualAmount ?? 0);

            var profilesByStatus = profiles
                .GroupBy(p => p.Status)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());

            var recentActivity = await _context.BrowserProfiles
                .Where(p => p.UserId == userId && p.LastStartedAt.HasValue)
                .OrderByDescending(p => p.LastStartedAt)
                .Take(5)
                .Select(p => new
                {
                    profileId = p.Id,
                    profileName = p.Name,
                    lastStartedAt = p.LastStartedAt,
                    status = p.Status.ToString()
                })
                .ToListAsync();

            return Ok(new
            {
                profiles = new
                {
                    total = profiles.Count,
                    byStatus = profilesByStatus,
                    createdLast30Days = profiles.Count(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                },
                payments = new
                {
                    total = payments.Count,
                    completed = payments.Count(p => p.Status == PaymentStatus.Completed),
                    totalSpent = totalSpent,
                    lastPayment = payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.CreatedAt
                },
                deposits = new
                {
                    total = deposits.Count,
                    completed = deposits.Count(d => d.Status == "completed"),
                    pending = deposits.Count(d => d.Status == "pending"),
                    expired = deposits.Count(d => d.Status == "expired"),
                    totalDeposited = totalDeposited,
                    lastDeposit = deposits.OrderByDescending(d => d.CreatedAt).FirstOrDefault()?.CreatedAt
                },
                recentActivity = recentActivity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user statistics");
            return StatusCode(500, new { message = "Failed to get statistics" });
        }
    }

    /// <summary>
    /// Обновить информацию профиля
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Проверка уникальности username
            if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.Id != userId);
                if (existingUser != null)
                {
                    return BadRequest(new { message = "Username already taken" });
                }
                user.Username = request.Username;
            }

            // Проверка уникальности email
            if (!string.IsNullOrWhiteSpace(request.Email) && request.Email != user.Email)
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email && u.Id != userId);
                if (existingUser != null)
                {
                    return BadRequest(new { message = "Email already taken" });
                }
                user.Email = request.Email;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Profile updated for user {UserId}", userId);
            return Ok(new { message = "Profile updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, new { message = "Failed to update profile" });
        }
    }

    /// <summary>
    /// Изменить пароль
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Current password and new password are required" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "New password must be at least 6 characters" });
            }

            var userId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Проверка текущего пароля
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Current password is incorrect" });
            }

            // Хеширование нового пароля
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Password changed for user {UserId}", userId);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "Failed to change password" });
        }
    }
}

public class UpdateUserProfileRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
