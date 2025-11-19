using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(ApplicationDbContext context, ILogger<ProfileService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<BrowserProfile> Profiles, int TotalCount)> GetProfilesAsync(
        int page, 
        int pageSize, 
        int? userId, 
        ProfileStatus? status, 
        int? serverId)
    {
        var query = _context.BrowserProfiles
            .Include(p => p.User)
            .Include(p => p.ServerNode)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(p => p.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (serverId.HasValue)
            query = query.Where(p => p.ServerNodeId == serverId.Value);

        var total = await query.CountAsync();
        var profiles = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (profiles, total);
    }

    public async Task<BrowserProfile?> GetProfileByIdAsync(int id)
    {
        return await _context.BrowserProfiles
            .Include(p => p.User)
            .Include(p => p.ServerNode)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public Task<bool> StartProfileAsync(int id)
    {
        // TODO: Implement profile start logic via API
        _logger.LogWarning("StartProfileAsync not implemented");
        return Task.FromResult(true);
    }

    public Task<bool> StopProfileAsync(int id)
    {
        // TODO: Implement profile stop logic via API
        _logger.LogWarning("StopProfileAsync not implemented");
        return Task.FromResult(true);
    }

    public async Task<bool> DeleteProfileAsync(int id)
    {
        try
        {
            var profile = await _context.BrowserProfiles.FindAsync(id);
            if (profile == null) return false;

            _context.BrowserProfiles.Remove(profile);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile");
            return false;
        }
    }

    public async Task<ProfileStats> GetProfileStatsAsync(int id)
    {
        var profile = await _context.BrowserProfiles
            .Include(p => p.ServerNode)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (profile == null)
            return new ProfileStats();

        return new ProfileStats
        {
            TotalRunTime = profile.TotalRunTime,
            StartCount = profile.StartCount,
            LastStarted = profile.LastStartedAt,
            ServerLocation = profile.ServerNode?.IpAddress ?? "Unknown"
        };
    }

    public async Task<List<AuditLog>> GetProfileLogsAsync(int id, int page, int pageSize)
    {
        return await _context.AuditLogs
            .Where(l => l.Category == LogCategory.ProfileManagement && l.EntityId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
