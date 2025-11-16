using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
            var totalProfiles = await _context.BrowserProfiles.CountAsync();
            var runningProfiles = await _context.BrowserProfiles
                .CountAsync(p => p.Status == ProfileStatus.Running);
            var totalNodes = await _context.ServerNodes.CountAsync();
            var healthyNodes = await _context.ServerNodes.CountAsync(n => n.IsHealthy);
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);

            return Ok(new
            {
                totalUsers,
                activeUsers,
                totalProfiles,
                runningProfiles,
                totalNodes,
                healthyNodes,
                totalRevenue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin stats");
            return StatusCode(500, new { message = "Failed to get stats", error = ex.Message });
        }
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Include(u => u.Subscription)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.IsAdmin,
                    u.IsActive,
                    u.TwoFactorEnabled,
                    u.CreatedAt,
                    u.LastLoginAt,
                    Subscription = u.Subscription != null ? new
                    {
                        u.Subscription.Tier,
                        u.Subscription.MaxProfiles,
                        u.Subscription.IsActive,
                        u.Subscription.EndDate
                    } : null
                })
                .ToListAsync();

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, new { message = "Failed to get users", error = ex.Message });
        }
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.BrowserProfiles)
                .Include(u => u.Payments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.IsAdmin,
                user.IsActive,
                user.TwoFactorEnabled,
                user.CreatedAt,
                user.LastLoginAt,
                Subscription = user.Subscription,
                ProfileCount = user.BrowserProfiles.Count,
                RunningProfiles = user.BrowserProfiles.Count(p => p.Status == ProfileStatus.Running),
                TotalPayments = user.Payments.Sum(p => p.Amount)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", id);
            return StatusCode(500, new { message = "Failed to get user", error = ex.Message });
        }
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            user.IsActive = request.IsActive;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} status updated to {Status}", id, request.IsActive);
            return Ok(new { message = "User status updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user status");
            return StatusCode(500, new { message = "Failed to update user status", error = ex.Message });
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.BrowserProfiles)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Предотвращаем удаление админа
            if (user.IsAdmin)
            {
                return BadRequest(new { message = "Cannot delete admin user" });
            }

            // Удаляем все профили пользователя
            _context.BrowserProfiles.RemoveRange(user.BrowserProfiles);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deleted", id);
            return Ok(new { message = "User deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(500, new { message = "Failed to delete user", error = ex.Message });
        }
    }

    [HttpGet("servers")]
    public async Task<IActionResult> GetServers()
    {
        try
        {
            var servers = await _context.ServerNodes.ToListAsync();
            return Ok(servers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting servers");
            return StatusCode(500, new { message = "Failed to get servers", error = ex.Message });
        }
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments()
    {
        try
        {
            var payments = await _context.Payments
                .Include(p => p.User)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    Username = p.User.Username,
                    p.Amount,
                    p.Currency,
                    p.Provider,
                    p.Status,
                    p.TransactionId,
                    p.CreatedAt,
                    p.CompletedAt
                })
                .OrderByDescending(p => p.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payments");
            return StatusCode(500, new { message = "Failed to get payments", error = ex.Message });
        }
    }

    [HttpPost("users/{id}/subscription")]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] UpdateSubscriptionRequest request)
    {
        try
        {
            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == id);

            if (subscription == null)
            {
                subscription = new Subscription
                {
                    UserId = id,
                    StartDate = DateTime.UtcNow
                };
                _context.Subscriptions.Add(subscription);
            }

            subscription.Tier = request.Tier;
            subscription.MaxProfiles = request.MaxProfiles;
            subscription.IsActive = request.IsActive;
            subscription.EndDate = request.EndDate;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription updated for user {UserId}", id);
            return Ok(new { message = "Subscription updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subscription for user {UserId}", id);
            return StatusCode(500, new { message = "Failed to update subscription", error = ex.Message });
        }
    }

    [HttpGet("profiles")]
    public async Task<IActionResult> GetAllProfiles([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var totalProfiles = await _context.BrowserProfiles.CountAsync();
            var profiles = await _context.BrowserProfiles
                .Include(p => p.User)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.UserId,
                    Username = p.User.Username,
                    p.Status,
                    p.ServerNodeIp,
                    p.Port,
                    p.CreatedAt,
                    p.LastStartedAt
                })
                .ToListAsync();

            return Ok(new
            {
                profiles,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalProfiles / (double)pageSize),
                totalProfiles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all profiles");
            return StatusCode(500, new { message = "Failed to get profiles", error = ex.Message });
        }
    }
}

public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public class UpdateSubscriptionRequest
{
    public SubscriptionTier Tier { get; set; }
    public int MaxProfiles { get; set; }
    public bool IsActive { get; set; }
    public DateTime? EndDate { get; set; }
}