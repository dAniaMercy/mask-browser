using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MaskAdmin.Services;

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProfileService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ProfileService(
        ApplicationDbContext context,
        ILogger<ProfileService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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

    public async Task<bool> StartProfileAsync(int id)
    {
        try
        {
            var profile = await _context.BrowserProfiles.FindAsync(id);
            if (profile == null)
            {
                _logger.LogWarning("Profile {ProfileId} not found", id);
                return false;
            }

            // Check if already running
            if (profile.Status == ProfileStatus.Running)
            {
                _logger.LogInformation("Profile {ProfileId} is already running", id);
                return true;
            }

            // Update status to Starting
            profile.Status = ProfileStatus.Starting;
            await _context.SaveChangesAsync();

            // Call main API to start the profile
            var apiBaseUrl = _configuration["MaskBrowserAPI:BaseUrl"] ?? "http://localhost:5050";
            var client = _httpClientFactory.CreateClient();

            var response = await client.PostAsync($"{apiBaseUrl}/api/profiles/{id}/start", null);

            if (response.IsSuccessStatusCode)
            {
                // Update profile status to Running
                profile.Status = ProfileStatus.Running;
                profile.LastStartedAt = DateTime.UtcNow;
                profile.StartCount++;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Profile {ProfileId} started successfully", id);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to start profile {ProfileId}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);

                // Set status to Error
                profile.Status = ProfileStatus.Error;
                await _context.SaveChangesAsync();

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profile {ProfileId}", id);

            // Try to update status to Error
            try
            {
                var profile = await _context.BrowserProfiles.FindAsync(id);
                if (profile != null)
                {
                    profile.Status = ProfileStatus.Error;
                    await _context.SaveChangesAsync();
                }
            }
            catch { /* Ignore errors when updating status */ }

            return false;
        }
    }

    public async Task<bool> StopProfileAsync(int id)
    {
        try
        {
            var profile = await _context.BrowserProfiles.FindAsync(id);
            if (profile == null)
            {
                _logger.LogWarning("Profile {ProfileId} not found", id);
                return false;
            }

            // Check if already stopped
            if (profile.Status == ProfileStatus.Stopped)
            {
                _logger.LogInformation("Profile {ProfileId} is already stopped", id);
                return true;
            }

            // Update status to Stopping
            profile.Status = ProfileStatus.Stopping;
            await _context.SaveChangesAsync();

            // Call main API to stop the profile
            var apiBaseUrl = _configuration["MaskBrowserAPI:BaseUrl"] ?? "http://localhost:5050";
            var client = _httpClientFactory.CreateClient();

            var response = await client.PostAsync($"{apiBaseUrl}/api/profiles/{id}/stop", null);

            if (response.IsSuccessStatusCode)
            {
                // Update profile status to Stopped and calculate runtime
                var now = DateTime.UtcNow;
                if (profile.LastStartedAt.HasValue)
                {
                    var runtime = (int)(now - profile.LastStartedAt.Value).TotalMinutes;
                    profile.TotalRunTime += runtime;
                }

                profile.Status = ProfileStatus.Stopped;
                profile.LastStoppedAt = now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Profile {ProfileId} stopped successfully", id);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to stop profile {ProfileId}: {StatusCode} - {Error}",
                    id, response.StatusCode, errorContent);

                // Set status to Error
                profile.Status = ProfileStatus.Error;
                await _context.SaveChangesAsync();

                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profile {ProfileId}", id);

            // Try to update status to Error
            try
            {
                var profile = await _context.BrowserProfiles.FindAsync(id);
                if (profile != null)
                {
                    profile.Status = ProfileStatus.Error;
                    await _context.SaveChangesAsync();
                }
            }
            catch { /* Ignore errors when updating status */ }

            return false;
        }
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
