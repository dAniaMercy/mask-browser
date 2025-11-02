using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    public async Task<IActionResult> GetProfiles()
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var profiles = await _profileService.GetUserProfilesAsync(userId);
        return Ok(profiles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var profile = await _profileService.GetProfileAsync(id, userId);
        if (profile == null)
        {
            return NotFound();
        }
        return Ok(profile);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest request)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        try
        {
            var profile = await _profileService.CreateProfileAsync(userId, request.Name, request.Config);
            if (profile == null)
            {
                return BadRequest(new { message = "Failed to create profile" });
            }
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartProfile(int id)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _profileService.StartProfileAsync(id, userId);
        if (!result)
        {
            return BadRequest(new { message = "Failed to start profile" });
        }
        return Ok(new { message = "Profile started" });
    }

    [HttpPost("{id}/stop")]
    public async Task<IActionResult> StopProfile(int id)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _profileService.StopProfileAsync(id, userId);
        if (!result)
        {
            return BadRequest(new { message = "Failed to stop profile" });
        }
        return Ok(new { message = "Profile stopped" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProfile(int id)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _profileService.DeleteProfileAsync(id, userId);
        if (!result)
        {
            return NotFound();
        }
        return Ok(new { message = "Profile deleted" });
    }
}

public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public BrowserConfig Config { get; set; } = new();
}

