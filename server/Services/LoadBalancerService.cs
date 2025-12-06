using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class LoadBalancerService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoadBalancerService> _logger;
    private readonly KafkaService _kafkaService;
    private readonly RabbitMQService _rabbitMQService;

    public LoadBalancerService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<LoadBalancerService> logger,
        KafkaService kafkaService,
        RabbitMQService rabbitMQService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _kafkaService = kafkaService;
        _rabbitMQService = rabbitMQService;
    }

    public async Task<ServerNode?> SelectNodeAsync()
    {
        var maxContainersPerNode = _configuration.GetValue<int>("LoadBalancer:MaxContainersPerNode", 1000);
        var healthCheckInterval = TimeSpan.FromSeconds(
            _configuration.GetValue<int>("LoadBalancer:HealthCheckIntervalSeconds", 30)
        );

        _logger.LogDebug("Selecting node with health check interval: {Interval} seconds", healthCheckInterval.TotalSeconds);

        var allNodes = await _context.ServerNodes.ToListAsync();
        _logger.LogDebug("Total nodes in database: {Count}", allNodes.Count);

        var healthyNodes = await _context.ServerNodes
            .Where(n => n.IsHealthy &&
                       n.ActiveContainers < n.MaxContainers &&
                       DateTime.UtcNow - n.LastHealthCheck < healthCheckInterval)
            .OrderBy(n => n.ActiveContainers)
            .ThenBy(n => n.CpuUsage)
            .ToListAsync();

        _logger.LogDebug("Healthy nodes found: {Count}", healthyNodes.Count);

        var node = healthyNodes.FirstOrDefault();

        if (node == null)
        {
            _logger.LogWarning("No healthy nodes available. Checking all nodes...");
            foreach (var n in allNodes)
            {
                var timeSinceCheck = DateTime.UtcNow - n.LastHealthCheck;
                _logger.LogWarning("Node {Ip}: Healthy={Healthy}, Active={Active}/{Max}, LastCheck={LastCheck} ({Age}s ago)", 
                    n.IpAddress, n.IsHealthy, n.ActiveContainers, n.MaxContainers, n.LastHealthCheck, timeSinceCheck.TotalSeconds);
            }
            return null;
        }

        _logger.LogInformation(
            "Selected node: {NodeIp} (Containers: {Active}/{Max}, CPU: {Cpu}%)",
            node.IpAddress, node.ActiveContainers, node.MaxContainers, node.CpuUsage
        );

        return node;
    }

    public async Task UpdateNodeHealthAsync(string nodeIp, bool isHealthy, double cpuUsage, double memoryUsage)
    {
        var node = await _context.ServerNodes
            .FirstOrDefaultAsync(n => n.IpAddress == nodeIp);

        if (node != null)
        {
            node.IsHealthy = isHealthy;
            node.CpuUsage = cpuUsage;
            node.MemoryUsage = memoryUsage;
            node.LastHealthCheck = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RegisterNodeAsync(string name, string ipAddress, int maxContainers)
    {
        var existingNode = await _context.ServerNodes
            .FirstOrDefaultAsync(n => n.IpAddress == ipAddress);

        if (existingNode == null)
        {
            var node = new ServerNode
            {
                Name = name,
                IpAddress = ipAddress,
                MaxContainers = maxContainers,
                IsHealthy = true,
                LastHealthCheck = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.ServerNodes.Add(node);
            await _context.SaveChangesAsync();
            
            // Publish event to Kafka (неблокирующе)
            _ = Task.Run(async () =>
            {
                try
                {
            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "NodeRegistered",
                NodeIp = ipAddress,
                NodeName = name,
                MaxContainers = maxContainers,
                Timestamp = DateTime.UtcNow
            });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish node registration to Kafka");
                }
            });
            
            // Publish to RabbitMQ for instant notification (неблокирующе)
            try
            {
            _rabbitMQService.Publish("scaling.node.registered", new
            {
                Ip = ipAddress,
                Name = name,
                Capacity = maxContainers
            });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish node registration to RabbitMQ");
            }
            
            _logger.LogInformation("Node registered: {NodeIp}", ipAddress);
        }
        else
        {
            existingNode.MaxContainers = maxContainers;
            existingNode.IsHealthy = true;
            existingNode.LastHealthCheck = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            // Publish event to Kafka (неблокирующе)
            _ = Task.Run(async () =>
            {
                try
                {
            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "NodeUpdated",
                NodeIp = ipAddress,
                MaxContainers = maxContainers,
                Timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish node update to Kafka");
                }
            });
            
            _logger.LogInformation("Node updated: {NodeIp}", ipAddress);
        }
    }
}

