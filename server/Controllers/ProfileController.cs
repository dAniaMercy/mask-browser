using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MaskBrowser.Server.Models;
using MaskBrowser.Server.Services;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ProfileService _profileService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(ProfileService profileService, ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _logger = logger;
    }

    private object ToDto(BrowserProfile p) => new
    {
        p.Id,
        p.UserId,
        p.Name,
        p.ContainerId,
        p.ServerNodeIp,
        p.Port,
        Config = new
        {
            p.Config.UserAgent,
            p.Config.ScreenResolution,
            p.Config.Timezone,
            p.Config.Language,
            p.Config.WebRTC,
            p.Config.Canvas,
            p.Config.WebGL
        },
        Status = p.Status.ToString(),
        p.CreatedAt,
        p.LastStartedAt
    };

    [HttpGet]
    public async Task<IActionResult> GetProfiles()
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var profiles = await _profileService.GetUserProfilesAsync(userId);
            var dto = profiles.Select(p => ToDto(p));
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profiles");
            return StatusCode(500, new { message = "Failed to get profiles" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var profile = await _profileService.GetProfileAsync(id, userId);
            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }
            return Ok(ToDto(profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile {ProfileId}", id);
            return StatusCode(500, new { message = "Failed to get profile" });
        }
    }

    [HttpPost]
    [EnableRateLimiting("profile-creation")]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Creating profile for user {UserId}: {ProfileName}", userId, request.Name);

            // Валидация
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Profile name is required" });
            }

            // Создаем профиль
            var profile = await _profileService.CreateProfileAsync(userId, request.Name, request.Config);

            if (profile == null)
            {
                return BadRequest(new { message = "Failed to create profile. Check your subscription limits." });
            }

            _logger.LogInformation("Profile created successfully: {ProfileId}", profile.Id);
            return Ok(ToDto(profile));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Profile limit reached for user");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile");
            return StatusCode(500, new { message = "Failed to create profile" });
        }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartProfile(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Starting profile {ProfileId} for user {UserId}", id, userId);

            var result = await _profileService.StartProfileAsync(id, userId);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to start profile {ProfileId}: {Reason}", id, result.ErrorMessage);
                return BadRequest(new { message = result.ErrorMessage ?? "Failed to start profile" });
            }

            _logger.LogInformation("Profile {ProfileId} started successfully", id);
            return Ok(new { message = "Profile started", profile = ToDto(result.Profile!) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profile {ProfileId}", id);
            return StatusCode(500, new { message = "Failed to start profile", error = ex.Message });
        }
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopProfile(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Stopping profile {ProfileId} for user {UserId}", id, userId);

            var result = await _profileService.StopProfileAsync(id, userId);

            if (!result)
            {
                return BadRequest(new { message = "Failed to stop profile. Profile may not be running or not found." });
            }

            _logger.LogInformation("Profile {ProfileId} stopped successfully", id);
            return Ok(new { message = "Profile stopped" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profile {ProfileId}", id);
            return StatusCode(500, new { message = "Failed to stop profile" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Deleting profile {ProfileId} for user {UserId}", id, userId);

            var result = await _profileService.DeleteProfileAsync(id, userId);

            if (!result)
            {
                return NotFound(new { message = "Profile not found" });
            }

            _logger.LogInformation("Profile {ProfileId} deleted successfully", id);
            return Ok(new { message = "Profile deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting profile {ProfileId}", id);
            return StatusCode(500, new { message = "Failed to delete profile" });
        }
    }

    [HttpPost("{id}/reset-error")]
    public async Task<IActionResult> ResetProfileError(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Resetting error status for profile {ProfileId} for user {UserId}", id, userId);

            var result = await _profileService.ResetProfileErrorAsync(id, userId);

            if (!result)
            {
                return BadRequest(new { message = "Failed to reset error status. Profile may not be in error state." });
            }

            _logger.LogInformation("Profile {ProfileId} error status reset successfully", id);
            return Ok(new { message = "Error status reset. Profile can be started again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting profile {ProfileId} error status", id);
            return StatusCode(500, new { message = "Failed to reset error status" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            _logger.LogInformation("Updating profile {ProfileId} for user {UserId}", id, userId);

            var profile = await _profileService.GetProfileAsync(id, userId);
            if (profile == null)
            {
                return NotFound(new { message = "Profile not found" });
            }

            // Обновляем только если профиль остановлен
            if (profile.Status != ProfileStatus.Stopped)
            {
                return BadRequest(new { message = "Cannot update running profile. Stop it first." });
            }

            profile.Name = request.Name ?? profile.Name;
            profile.Config = request.Config ?? profile.Config;

            await _profileService.UpdateProfileAsync(profile);

            _logger.LogInformation("Profile {ProfileId} updated successfully", id);
            return Ok(ToDto(profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile {ProfileId}", id);
            return StatusCode(500, new { message = "Failed to update profile" });
        }
    }
}

public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public BrowserConfig Config { get; set; } = new();
}

public class UpdateProfileRequest
{
    public string? Name { get; set; }
    public BrowserConfig? Config { get; set; }
}