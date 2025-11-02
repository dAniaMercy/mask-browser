using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class ProfileService
{
    private readonly ApplicationDbContext _context;
    private readonly DockerService _dockerService;
    private readonly LoadBalancerService _loadBalancerService;
    private readonly RabbitMQService _rabbitMQ;
    private readonly KafkaService _kafkaService;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        ApplicationDbContext context,
        DockerService dockerService,
        LoadBalancerService loadBalancerService,
        RabbitMQService rabbitMQ,
        KafkaService kafkaService,
        ILogger<ProfileService> logger)
    {
        _context = context;
        _dockerService = dockerService;
        _loadBalancerService = loadBalancerService;
        _rabbitMQ = rabbitMQ;
        _kafkaService = kafkaService;
        _logger = logger;
    }

    public async Task<List<BrowserProfile>> GetUserProfilesAsync(int userId)
    {
        return await _context.BrowserProfiles
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<BrowserProfile?> GetProfileAsync(int profileId, int userId)
    {
        return await _context.BrowserProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
    }

    public async Task<BrowserProfile?> CreateProfileAsync(int userId, string name, BrowserConfig config)
    {
        var user = await _context.Users
            .Include(u => u.Subscription)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || user.Subscription == null)
        {
            return null;
        }

        // Check profile limit
        var profileCount = await _context.BrowserProfiles
            .CountAsync(p => p.UserId == userId);

        if (profileCount >= user.Subscription.MaxProfiles)
        {
            throw new InvalidOperationException("Profile limit reached");
        }

        var profile = new BrowserProfile
        {
            UserId = userId,
            Name = name,
            Config = config,
            Status = ProfileStatus.Stopped,
            CreatedAt = DateTime.UtcNow
        };

        _context.BrowserProfiles.Add(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Profile created: {ProfileId} for user {UserId}", profile.Id, userId);
        return profile;
    }

    public async Task<bool> StartProfileAsync(int profileId, int userId)
    {
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null || profile.Status == ProfileStatus.Running)
        {
            return false;
        }

        // Select server node
        var node = await _loadBalancerService.SelectNodeAsync();
        if (node == null)
        {
            return false;
        }

        profile.Status = ProfileStatus.Starting;
        await _context.SaveChangesAsync();

        try
        {
            var containerId = await _dockerService.CreateBrowserContainerAsync(
                profileId,
                profile.Config,
                node.IpAddress
            );

            profile.ContainerId = containerId;
            profile.ServerNodeIp = node.IpAddress;
            profile.Status = ProfileStatus.Running;
            profile.LastStartedAt = DateTime.UtcNow;

            // Get available port
            profile.Port = await _dockerService.GetContainerPortAsync(containerId);

            node.ActiveContainers++;
            await _context.SaveChangesAsync();

            // Publish to RabbitMQ for instant task processing
            _rabbitMQ.Publish("container.started", new { ProfileId = profileId, ContainerId = containerId });

            // Publish to Kafka for analytics and logging
            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "ContainerStarted",
                ProfileId = profileId,
                ContainerId = containerId,
                NodeIp = node.IpAddress,
                Timestamp = DateTime.UtcNow
            });

            await _kafkaService.PublishContainerLogAsync(containerId, $"Container {containerId} started for profile {profileId} on node {node.IpAddress}");

            _logger.LogInformation("Profile started: {ProfileId} on {NodeIp}", profileId, node.IpAddress);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start profile {ProfileId}", profileId);
            profile.Status = ProfileStatus.Error;
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> StopProfileAsync(int profileId, int userId)
    {
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null || string.IsNullOrEmpty(profile.ContainerId))
        {
            return false;
        }

        profile.Status = ProfileStatus.Stopping;
        await _context.SaveChangesAsync();

        try
        {
            await _dockerService.StopContainerAsync(profile.ContainerId);

            var node = await _context.ServerNodes
                .FirstOrDefaultAsync(n => n.IpAddress == profile.ServerNodeIp);
            if (node != null)
            {
                node.ActiveContainers = Math.Max(0, node.ActiveContainers - 1);
            }

            profile.Status = ProfileStatus.Stopped;
            var containerId = profile.ContainerId;
            profile.ContainerId = string.Empty;
            await _context.SaveChangesAsync();

            // Publish to RabbitMQ for instant task processing
            _rabbitMQ.Publish("container.stopped", new { ProfileId = profileId });

            // Publish to Kafka for analytics
            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "ContainerStopped",
                ProfileId = profileId,
                ContainerId = containerId,
                Timestamp = DateTime.UtcNow
            });

            await _kafkaService.PublishContainerLogAsync(containerId, $"Container {containerId} stopped for profile {profileId}");

            _logger.LogInformation("Profile stopped: {ProfileId}", profileId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop profile {ProfileId}", profileId);
            profile.Status = ProfileStatus.Error;
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> DeleteProfileAsync(int profileId, int userId)
    {
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null)
        {
            return false;
        }

        if (profile.Status == ProfileStatus.Running)
        {
            await StopProfileAsync(profileId, userId);
        }

        if (!string.IsNullOrEmpty(profile.ContainerId))
        {
            try
            {
                await _dockerService.DeleteContainerAsync(profile.ContainerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete container {ContainerId}", profile.ContainerId);
            }
        }

        _context.BrowserProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Profile deleted: {ProfileId}", profileId);
        return true;
    }
}

