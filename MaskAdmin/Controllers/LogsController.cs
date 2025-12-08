using MaskAdmin.Models;
using MaskAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaskAdmin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class LogsController : Controller
{
    private readonly ILogService _logService;
    private readonly IExportService _exportService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        ILogService logService,
        IExportService exportService,
        ILogger<LogsController> logger)
    {
        _logService = logService;
        _exportService = exportService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 50,
        string? search = null,
        string? category = null,
        string? level = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        try
        {
            LogCategory? categoryEnum = null;
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<LogCategory>(category, out var parsedCategory))
                categoryEnum = parsedCategory;

            AuditLogLevel? levelEnum = null;
            if (!string.IsNullOrEmpty(level) && Enum.TryParse<AuditLogLevel>(level, out var parsedLevel))
                levelEnum = parsedLevel;

            var (logs, totalCount) = await _logService.GetLogsAsync(
                page,
                pageSize,
                categoryEnum,
                levelEnum,
                dateFrom,
                dateTo,
                search);

            var model = (
                Logs: logs,
                TotalCount: totalCount,
                CurrentPage: page,
                PageSize: pageSize,
                Search: search ?? string.Empty,
                Category: category ?? string.Empty,
                Level: level ?? string.Empty,
                DateFrom: dateFrom,
                DateTo: dateTo
            );

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit logs");
            TempData["Error"] = "Failed to load audit logs";
            return View((
                Logs: new List<AuditLog>(),
                TotalCount: 0,
                CurrentPage: 1,
                PageSize: pageSize,
                Search: string.Empty,
                Category: string.Empty,
                Level: string.Empty,
                DateFrom: (DateTime?)null,
                DateTo: (DateTime?)null
            ));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        string format = "csv",
        string? search = null,
        string? category = null,
        string? level = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        try
        {
            // Get all matching logs (no pagination for export)
            LogCategory? categoryEnum = null;
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<LogCategory>(category, out var parsedCategory))
                categoryEnum = parsedCategory;

            AuditLogLevel? levelEnum = null;
            if (!string.IsNullOrEmpty(level) && Enum.TryParse<AuditLogLevel>(level, out var parsedLevel))
                levelEnum = parsedLevel;

            var (logs, _) = await _logService.GetLogsAsync(
                page: 1,
                pageSize: 100000, // Large number to get all
                categoryEnum,
                levelEnum,
                dateFrom,
                dateTo,
                search);

            // Convert to export format
            var exportData = logs.Select(log => new
            {
                Timestamp = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = log.Level.ToString(),
                Category = log.Category.ToString(),
                User = log.User?.Username ?? "System",
                Action = log.Action,
                Entity = log.Entity,
                EntityId = log.EntityId?.ToString() ?? "",
                IpAddress = log.IpAddress,
                UserAgent = log.UserAgent,
                OldValues = log.OldValues ?? "",
                NewValues = log.NewValues ?? "",
                AdditionalData = log.AdditionalData ?? ""
            }).ToList();

            byte[] fileBytes;
            string fileName;
            string contentType;

            var headers = new[] {
                "Timestamp", "Level", "Category", "User", "Action",
                "Entity", "EntityId", "IpAddress", "UserAgent",
                "OldValues", "NewValues", "AdditionalData"
            };

            if (format.ToLower() == "excel")
            {
                fileBytes = await _exportService.ExportToExcelAsync(exportData, "AuditLogs", headers);
                fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            else
            {
                fileBytes = await _exportService.ExportToCsvAsync(exportData, headers);
                fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                contentType = "text/csv";
            }

            return File(fileBytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs");
            TempData["Error"] = "Failed to export audit logs";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> UserLogs(int userId, int page = 1, int pageSize = 20)
    {
        try
        {
            var (logs, totalCount) = await _logService.GetLogsAsync(
                page,
                pageSize,
                category: null,
                level: null,
                from: null,
                to: null,
                search: null);

            // Filter by user (this should be done in the service, but for now...)
            logs = logs.Where(l => l.UserId == userId).ToList();
            totalCount = logs.Count;

            var model = (
                Logs: logs,
                TotalCount: totalCount,
                CurrentPage: page,
                PageSize: pageSize,
                Search: string.Empty,
                Category: string.Empty,
                Level: string.Empty,
                DateFrom: (DateTime?)null,
                DateTo: (DateTime?)null
            );

            ViewData["Title"] = "User Audit Logs";
            return View("Index", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user audit logs for user {UserId}", userId);
            TempData["Error"] = "Failed to load user audit logs";
            return RedirectToAction("Details", "Users", new { id = userId });
        }
    }
}
