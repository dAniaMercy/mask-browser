using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(ApplicationDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<User> Users, int TotalCount)> GetUsersAsync(
        int page, 
        int pageSize, 
        string? search, 
        string? status, 
        string? sort)
    {
        try
        {
            var query = _context.Users
                .Include(u => u.Subscription)
                .Include(u => u.BrowserProfiles)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.Username.Contains(search) ||
                    u.Email.Contains(search));
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status.ToLower())
                {
                    case "active":
                        query = query.Where(u => u.IsActive && !u.IsBanned && !u.IsFrozen);
                        break;
                    case "banned":
                        query = query.Where(u => u.IsBanned);
                        break;
                    case "frozen":
                        query = query.Where(u => u.IsFrozen);
                        break;
                    case "inactive":
                        query = query.Where(u => !u.IsActive);
                        break;
                }
            }

            var totalCount = await query.CountAsync();

            // Sorting
            query = sort?.ToLower() switch
            {
                "username" => query.OrderBy(u => u.Username),
                "username_desc" => query.OrderByDescending(u => u.Username),
                "email" => query.OrderBy(u => u.Email),
                "email_desc" => query.OrderByDescending(u => u.Email),
                "created" => query.OrderBy(u => u.CreatedAt),
                "created_desc" => query.OrderByDescending(u => u.CreatedAt),
                "last_login" => query.OrderBy(u => u.LastLoginAt ?? DateTime.MinValue),
                "last_login_desc" => query.OrderByDescending(u => u.LastLoginAt ?? DateTime.MinValue),
                "balance" => query.OrderBy(u => u.Balance),
                "balance_desc" => query.OrderByDescending(u => u.Balance),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };

            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (users, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            throw;
        }
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.Subscription)
            .Include(u => u.BrowserProfiles)
            .Include(u => u.Payments)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> UpdateUserAsync(int id, User updatedUser)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            var oldValues = new
            {
                user.Username,
                user.Email,
                user.IsActive,
                user.IsAdmin
            };

            user.Username = updatedUser.Username;
            user.Email = updatedUser.Email;
            user.IsActive = updatedUser.IsActive;
            user.IsAdmin = updatedUser.IsAdmin;

            await _context.SaveChangesAsync();

            // Log the change
            await LogAuditAsync(
                userId: id,
                action: "UpdateUser",
                entity: "User",
                entityId: id,
                oldValues: System.Text.Json.JsonSerializer.Serialize(oldValues),
                newValues: System.Text.Json.JsonSerializer.Serialize(new
                {
                    updatedUser.Username,
                    updatedUser.Email,
                    updatedUser.IsActive,
                    updatedUser.IsAdmin
                })
            );

            _logger.LogInformation("User {UserId} updated successfully", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> AdjustBalanceAsync(int id, decimal amount, string reason)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            var oldBalance = user.Balance;
            user.Balance += amount;

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: id,
                action: "AdjustBalance",
                entity: "User",
                entityId: id,
                oldValues: oldBalance.ToString(),
                newValues: user.Balance.ToString(),
                additionalData: System.Text.Json.JsonSerializer.Serialize(new { amount, reason })
            );

            _logger.LogInformation("Balance adjusted for user {UserId}: {Amount} ({Reason})", id, amount, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting balance for user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> BanUserAsync(int id, string reason)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.IsBanned = true;
            user.IsActive = false; // Also deactivate the account

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: id,
                action: "BanUser",
                entity: "User",
                entityId: id,
                additionalData: System.Text.Json.JsonSerializer.Serialize(new { reason }),
                level: AuditLogLevel.Warning
            );

            _logger.LogWarning("User {UserId} banned: {Reason}", id, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> UnbanUserAsync(int id)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.IsBanned = false;
            user.IsActive = true; // Re-activate the account

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: id,
                action: "UnbanUser",
                entity: "User",
                entityId: id
            );

            _logger.LogInformation("User {UserId} unbanned", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbanning user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> FreezeUserAsync(int id, string reason)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.IsFrozen = true;

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: id,
                action: "FreezeUser",
                entity: "User",
                entityId: id,
                additionalData: System.Text.Json.JsonSerializer.Serialize(new { reason }),
                level: AuditLogLevel.Warning
            );

            _logger.LogWarning("User {UserId} frozen: {Reason}", id, reason);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freezing user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.BrowserProfiles)
                .Include(u => u.Subscription)
                .Include(u => u.Payments)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return false;

            // Check if user has active profiles
            if (user.BrowserProfiles.Any(p => p.Status == ProfileStatus.Running))
            {
                _logger.LogWarning("Cannot delete user {UserId} - has active profiles", id);
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: null,
                action: "DeleteUser",
                entity: "User",
                entityId: id,
                level: AuditLogLevel.Critical
            );

            _logger.LogWarning("User {UserId} deleted", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(int id, string newPassword)
    {
        try
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            await _context.SaveChangesAsync();

            await LogAuditAsync(
                userId: id,
                action: "ResetPassword",
                entity: "User",
                entityId: id,
                level: AuditLogLevel.Warning,
                category: LogCategory.Security
            );

            _logger.LogInformation("Password reset for user {UserId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", id);
            return false;
        }
    }

    public async Task<UserStats> GetUserStatsAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.BrowserProfiles)
            .Include(u => u.Payments)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return new UserStats();

        return new UserStats
        {
            TotalProfiles = user.BrowserProfiles.Count,
            ActiveProfiles = user.BrowserProfiles.Count(p => p.Status == ProfileStatus.Running),
            TotalSpent = user.Payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            TotalLogins = await _context.AuditLogs.CountAsync(l => l.UserId == id && l.Action == "Login"),
            LastLogin = user.LastLoginAt,
            RecentPayments = user.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .ToList()
        };
    }

    public async Task<List<AuditLog>> GetUserLogsAsync(int id, int page, int pageSize)
    {
        return await _context.AuditLogs
            .Where(l => l.UserId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    private async Task LogAuditAsync(
        int? userId,
        string action,
        string entity,
        int? entityId = null,
        string? oldValues = null,
        string? newValues = null,
        string? additionalData = null,
        AuditLogLevel level = AuditLogLevel.Info,
        LogCategory category = LogCategory.UserManagement)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                AdditionalData = additionalData,
                Level = level,
                Category = category,
                IpAddress = "Admin",
                UserAgent = "MaskAdmin",
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging audit");
        }
    }
}
