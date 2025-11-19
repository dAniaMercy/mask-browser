using MaskAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaskAdmin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService dashboardService,
        ILogger<DashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var dashboardData = await _dashboardService.GetDashboardDataAsync();
            return View(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            TempData["Error"] = "Failed to load dashboard data";
            return View();
        }
    }

    [HttpGet("api/dashboard/stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _dashboardService.GetStatsAsync();
            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats");
            return StatusCode(500, new { error = "Failed to get stats" });
        }
    }

    [HttpGet("api/dashboard/charts/profiles")]
    public async Task<IActionResult> GetProfilesChart([FromQuery] int days = 7)
    {
        try
        {
            var data = await _dashboardService.GetProfilesChartDataAsync(days);
            return Json(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profiles chart");
            return StatusCode(500, new { error = "Failed to get chart data" });
        }
    }

    [HttpGet("api/dashboard/charts/users")]
    public async Task<IActionResult> GetUsersChart([FromQuery] int days = 7)
    {
        try
        {
            var data = await _dashboardService.GetUsersChartDataAsync(days);
            return Json(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users chart");
            return StatusCode(500, new { error = "Failed to get chart data" });
        }
    }

    [HttpGet("api/dashboard/charts/revenue")]
    public async Task<IActionResult> GetRevenueChart([FromQuery] int days = 30)
    {
        try
        {
            var data = await _dashboardService.GetRevenueChartDataAsync(days);
            return Json(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue chart");
            return StatusCode(500, new { error = "Failed to get chart data" });
        }
    }
}
