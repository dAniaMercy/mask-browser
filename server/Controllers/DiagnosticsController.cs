using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Services;
using MaskBrowser.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class DiagnosticsController : ControllerBase
{
    private readonly DockerService _dockerService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        DockerService dockerService,
        ApplicationDbContext context,
        ILogger<DiagnosticsController> logger)
    {
        _dockerService = dockerService;
        _context = context;
        _logger = logger;
    }

    [HttpGet("containers")]
    public async Task<IActionResult> GetContainersDiagnostics()
    {
        try
        {
            var containers = await _dockerService.GetRunningContainersAsync();
            var diagnostics = new List<object>();

            foreach (var container in containers)
            {
                var healthStatus = await _dockerService.GetContainerHealthStatusAsync(container.ID);
                
                // Находим соответствующий профиль
                var profile = await _context.BrowserProfiles
                    .FirstOrDefaultAsync(p => p.ContainerId == container.ID);

                diagnostics.Add(new
                {
                    containerId = container.ID,
                    name = string.Join(", ", container.Names ?? Array.Empty<string>()),
                    status = container.Status,
                    health = new
                    {
                        isRunning = healthStatus.IsRunning,
                        healthStatus = healthStatus.HealthStatus,
                        uptime = healthStatus.Uptime.TotalSeconds,
                        port = healthStatus.Port
                    },
                    profile = profile != null ? new
                    {
                        profileId = profile.Id,
                        profileName = profile.Name,
                        userId = profile.UserId,
                        status = profile.Status.ToString()
                    } : null,
                    created = container.Created,
                    image = container.Image
                });
            }

            return Ok(new
            {
                totalContainers = containers.Count,
                containers = diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting containers diagnostics");
            return StatusCode(500, new { message = "Failed to get diagnostics", error = ex.Message });
        }
    }

    [HttpGet("containers/{containerId}/health")]
    public async Task<IActionResult> GetContainerHealth(string containerId)
    {
        try
        {
            var healthStatus = await _dockerService.GetContainerHealthStatusAsync(containerId);
            var isHealthy = await _dockerService.IsContainerHealthyAsync(containerId);

            return Ok(new
            {
                containerId = healthStatus.ContainerId,
                isHealthy,
                status = healthStatus.Status,
                healthStatus = healthStatus.HealthStatus,
                isRunning = healthStatus.IsRunning,
                startedAt = healthStatus.StartedAt,
                uptime = new
                {
                    totalSeconds = healthStatus.Uptime.TotalSeconds,
                    totalMinutes = healthStatus.Uptime.TotalMinutes,
                    totalHours = healthStatus.Uptime.TotalHours,
                    days = healthStatus.Uptime.Days
                },
                port = healthStatus.Port,
                exitCode = healthStatus.ExitCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container health for {ContainerId}", containerId);
            return StatusCode(500, new { message = "Failed to get container health", error = ex.Message });
        }
    }

    [HttpGet("profiles/sync-status")]
    public async Task<IActionResult> GetProfilesSyncStatus()
    {
        try
        {
            var runningProfiles = await _context.BrowserProfiles
                .Where(p => p.Status == ProfileStatus.Running && !string.IsNullOrEmpty(p.ContainerId))
                .ToListAsync();

            var containers = await _dockerService.GetRunningContainersAsync();
            var containerIds = containers.Select(c => c.ID).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var outOfSync = runningProfiles
                .Where(p => !containerIds.Contains(p.ContainerId))
                .Select(p => new
                {
                    profileId = p.Id,
                    profileName = p.Name,
                    containerId = p.ContainerId,
                    status = p.Status.ToString(),
                    userId = p.UserId
                })
                .ToList();

            var orphanedContainers = containers
                .Where(c => !runningProfiles.Any(p => p.ContainerId == c.ID))
                .Select(c => new
                {
                    containerId = c.ID,
                    name = string.Join(", ", c.Names ?? Array.Empty<string>()),
                    status = c.Status
                })
                .ToList();

            return Ok(new
            {
                totalRunningProfiles = runningProfiles.Count,
                totalRunningContainers = containers.Count,
                outOfSyncProfiles = outOfSync.Count,
                orphanedContainers = orphanedContainers.Count,
                details = new
                {
                    outOfSync = outOfSync,
                    orphaned = orphanedContainers
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profiles sync status");
            return StatusCode(500, new { message = "Failed to get sync status", error = ex.Message });
        }
    }

    [HttpPost("profiles/{profileId}/fix-status")]
    public async Task<IActionResult> FixProfileStatus(int profileId)
    {
        try
        {
            var profile = await _context.BrowserProfiles.FindAsync(profileId);
            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }

            if (string.IsNullOrEmpty(profile.ContainerId))
            {
                return BadRequest(new { message = "Profile has no container ID" });
            }

            var isHealthy = await _dockerService.IsContainerHealthyAsync(profile.ContainerId);
            
            if (profile.Status == ProfileStatus.Running && !isHealthy)
            {
                profile.Status = ProfileStatus.Error;
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    message = "Profile status updated to Error",
                    profileId = profile.Id,
                    previousStatus = "Running",
                    newStatus = "Error",
                    reason = "Container is not healthy"
                });
            }

            if (profile.Status == ProfileStatus.Error && isHealthy)
            {
                profile.Status = ProfileStatus.Running;
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    message = "Profile status updated to Running",
                    profileId = profile.Id,
                    previousStatus = "Error",
                    newStatus = "Running",
                    reason = "Container is healthy"
                });
            }

            return Ok(new
            {
                message = "No status change needed",
                profileId = profile.Id,
                currentStatus = profile.Status.ToString(),
                containerHealthy = isHealthy
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing profile status for {ProfileId}", profileId);
            return StatusCode(500, new { message = "Failed to fix profile status", error = ex.Message });
        }
    }
}
