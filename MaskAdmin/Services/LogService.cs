using MaskAdmin.Data;
using MaskAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskAdmin.Services;

public class LogService : ILogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LogService> _logger;

    public LogService(ApplicationDbContext context, ILogger<LogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(List<AuditLog> Logs, int TotalCount)> GetLogsAsync(
        int page, 
        int pageSize, 
        LogCategory? category, 
        AuditLogLevel? level, 
        DateTime? from, 
        DateTime? to, 
        string? search)
    {
        var query = _context.AuditLogs
            .Include(l => l.User)
            .AsQueryable();

        if (category.HasValue)
            query = query.Where(l => l.Category == category.Value);

        if (level.HasValue)
            query = query.Where(l => l.Level == level.Value);

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(l => 
                l.Action.Contains(search) || 
                l.Entity.Contains(search) ||
                (l.AdditionalData != null && l.AdditionalData.Contains(search)));

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, total);
    }

    public Task<byte[]> ExportLogsAsync(string format, LogCategory? category, DateTime? from, DateTime? to)
    {
        // TODO: Implement export logic
        _logger.LogWarning("ExportLogsAsync not implemented");
        return Task.FromResult(Array.Empty<byte>());
    }
}
