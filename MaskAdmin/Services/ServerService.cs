using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class ServerService : IServerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServerService> _logger;

    public ServerService(ApplicationDbContext context, ILogger<ServerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<ServerNode> Servers, int TotalCount)> GetServersAsync(int page, int pageSize)
    {
        var query = _context.ServerNodes;
        var total = await query.CountAsync();
        var servers = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (servers, total);
    }

    public async Task<ServerNode?> GetServerByIdAsync(int id)
    {
        return await _context.ServerNodes.FindAsync(id);
    }

    public async Task<bool> RegisterServerAsync(ServerNode server)
    {
        try
        {
            _context.ServerNodes.Add(server);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering server");
            return false;
        }
    }

    public async Task<bool> UpdateServerAsync(int id, ServerNode server)
    {
        try
        {
            var existing = await _context.ServerNodes.FindAsync(id);
            if (existing == null) return false;

            existing.Name = server.Name;
            existing.MaxContainers = server.MaxContainers;
            existing.IsEnabled = server.IsEnabled;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating server");
            return false;
        }
    }

    public async Task<bool> DeleteServerAsync(int id)
    {
        try
        {
            var server = await _context.ServerNodes.FindAsync(id);
            if (server == null) return false;

            _context.ServerNodes.Remove(server);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting server");
            return false;
        }
    }

    public Task<bool> RestartServerAsync(int id)
    {
        // TODO: Implement server restart logic
        _logger.LogWarning("RestartServerAsync not implemented");
        return Task.FromResult(true);
    }

    public async Task<List<BrowserProfile>> GetServerContainersAsync(int id)
    {
        return await _context.BrowserProfiles
            .Where(p => p.ServerNodeId == id)
            .ToListAsync();
    }

    public Task<ServerMetrics> GetServerMetricsAsync(int id)
    {
        // TODO: Implement real metrics collection
        return Task.FromResult(new ServerMetrics());
    }

    public async Task<List<AuditLog>> GetServerLogsAsync(int id, int page, int pageSize)
    {
        return await _context.AuditLogs
            .Where(l => l.Category == LogCategory.ServerManagement)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
