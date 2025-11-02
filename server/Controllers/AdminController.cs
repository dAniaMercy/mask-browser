using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;

    public AdminController(ILogger<AdminController> logger)
    {
        _logger = logger;
    }

    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        // TODO: Implement user management
        return Ok(new { message = "User list" });
    }

    [HttpGet("servers")]
    public IActionResult GetServers()
    {
        // TODO: Implement server monitoring
        return Ok(new { message = "Server list" });
    }

    [HttpGet("payments")]
    public IActionResult GetPayments()
    {
        // TODO: Implement payment management
        return Ok(new { message = "Payment list" });
    }
}

