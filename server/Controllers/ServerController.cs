using Microsoft.AspNetCore.Mvc;
using MaskBrowser.Server.Services;

namespace MaskBrowser.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServerController : ControllerBase
{
    private readonly LoadBalancerService _loadBalancerService;
    private readonly ILogger<ServerController> _logger;

    public ServerController(LoadBalancerService loadBalancerService, ILogger<ServerController> logger)
    {
        _loadBalancerService = loadBalancerService;
        _logger = logger;
    }

    [HttpPost]
    [Route("api/servers/register")]
    public async Task<IActionResult> RegisterNode([FromBody] ServerRegisterRequest request)
    {
        await _loadBalancerService.RegisterNodeAsync(
            $"node-{request.Ip}", 
            request.Ip, 
            request.Capacity
        );
        return Ok(new { message = "Node registered", ip = request.Ip });
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterNodeLegacy([FromBody] RegisterNodeRequest request)
    {
        await _loadBalancerService.RegisterNodeAsync(request.Name, request.IpAddress, request.MaxContainers);
        return Ok(new { message = "Node registered" });
    }

    [HttpPost("health")]
    public async Task<IActionResult> UpdateHealth([FromBody] HealthUpdateRequest request)
    {
        await _loadBalancerService.UpdateNodeHealthAsync(
            request.IpAddress,
            request.IsHealthy,
            request.CpuUsage,
            request.MemoryUsage
        );
        return Ok(new { message = "Health updated" });
    }
}

public class ServerRegisterRequest
{
    public string Ip { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1000;
    public string Role { get; set; } = "node";
}

public class RegisterNodeRequest
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int MaxContainers { get; set; } = 1000;
}

public class HealthUpdateRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
}

