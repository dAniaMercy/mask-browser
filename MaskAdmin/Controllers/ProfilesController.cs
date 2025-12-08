using MaskAdmin.Models;
using MaskAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaskAdmin.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ProfilesController : Controller
{
    private readonly IProfileService _profileService;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(IProfileService profileService, ILogger<ProfilesController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 50,
        int? userId = null,
        ProfileStatus? status = null,
        int? serverId = null)
    {
        try
        {
            var (profiles, totalCount) = await _profileService.GetProfilesAsync(
                page,
                pageSize,
                userId,
                status,
                serverId);

            var model = (
                Profiles: profiles,
                TotalCount: totalCount,
                CurrentPage: page,
                PageSize: pageSize,
                UserId: userId,
                Status: status,
                ServerId: serverId
            );

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profiles");
            TempData["Error"] = "Failed to load profiles";
            return View((
                Profiles: new List<BrowserProfile>(),
                TotalCount: 0,
                CurrentPage: 1,
                PageSize: pageSize,
                UserId: (int?)null,
                Status: (ProfileStatus?)null,
                ServerId: (int?)null
            ));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        try
        {
            var success = await _profileService.StartProfileAsync(id);

            if (success)
            {
                TempData["Success"] = $"Profile {id} started successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to start profile {id}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profile {ProfileId}", id);
            TempData["Error"] = $"Error starting profile {id}: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(int id)
    {
        try
        {
            var success = await _profileService.StopProfileAsync(id);

            if (success)
            {
                TempData["Success"] = $"Profile {id} stopped successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to stop profile {id}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profile {ProfileId}", id);
            TempData["Error"] = $"Error stopping profile {id}: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restart(int id)
    {
        try
        {
            // Stop first
            var stopSuccess = await _profileService.StopProfileAsync(id);
            if (!stopSuccess)
            {
                TempData["Error"] = $"Failed to stop profile {id} for restart";
                return RedirectToAction(nameof(Index));
            }

            // Wait a bit
            await Task.Delay(2000);

            // Start again
            var startSuccess = await _profileService.StartProfileAsync(id);

            if (startSuccess)
            {
                TempData["Success"] = $"Profile {id} restarted successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to start profile {id} after stopping";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting profile {ProfileId}", id);
            TempData["Error"] = $"Error restarting profile {id}: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var success = await _profileService.DeleteProfileAsync(id);

            if (success)
            {
                TempData["Success"] = $"Profile {id} deleted successfully";
            }
            else
            {
                TempData["Error"] = $"Failed to delete profile {id}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile {ProfileId}", id);
            TempData["Error"] = $"Error deleting profile {id}: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
