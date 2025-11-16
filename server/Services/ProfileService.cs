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
        _logger.LogInformation("📥 Getting profiles for user {UserId}", userId);
        var profiles = await _context.BrowserProfiles
            .Where(p => p.UserId == userId)
            .ToListAsync();
        _logger.LogInformation("✅ Found {Count} profiles for user {UserId}", profiles.Count, userId);
        return profiles;
    }

    public async Task<BrowserProfile?> GetProfileAsync(int profileId, int userId)
    {
        return await _context.BrowserProfiles
            .FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
    }

    public async Task<BrowserProfile?> CreateProfileAsync(int userId, string name, BrowserConfig config)
    {
        try
        {
            _logger.LogInformation("➕ Creating profile '{Name}' for user {UserId}", name, userId);
            
            // Проверка пользователя
            var user = await _context.Users
                .Include(u => u.Subscription)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("❌ User {UserId} not found", userId);
                return null;
            }

            _logger.LogInformation("✅ User found: {Username}", user.Username);

            // Создаем бесплатную подписку если её нет
            if (user.Subscription == null)
            {
                _logger.LogInformation("📦 Creating free subscription for user {UserId}", userId);
                user.Subscription = new Subscription
                {
                    UserId = userId,
                    Tier = SubscriptionTier.Free,
                    MaxProfiles = 3, // Бесплатно 3 профиля
                    StartDate = DateTime.UtcNow,
                    IsActive = true
                };
                _context.Subscriptions.Add(user.Subscription);
                
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("✅ Free subscription created");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to create subscription");
                    throw;
                }
            }

            _logger.LogInformation("📊 Subscription: Tier={Tier}, Max={Max}", 
                user.Subscription.Tier, user.Subscription.MaxProfiles);

            // Проверка лимитов
            var profileCount = await _context.BrowserProfiles
                .CountAsync(p => p.UserId == userId);

            _logger.LogInformation("📈 Current profiles: {Current}/{Max}", 
                profileCount, user.Subscription.MaxProfiles);

            if (profileCount >= user.Subscription.MaxProfiles)
            {
                _logger.LogWarning("⚠️ Profile limit reached for user {UserId}: {Current}/{Max}", 
                    userId, profileCount, user.Subscription.MaxProfiles);
                throw new InvalidOperationException($"Profile limit reached ({user.Subscription.MaxProfiles})");
            }

            // Валидация config
            if (config == null)
            {
                _logger.LogWarning("⚠️ Config is null, creating default");
                config = new BrowserConfig
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                    ScreenResolution = "1920x1080",
                    Timezone = "UTC",
                    Language = "en-US",
                    WebRTC = false,
                    Canvas = false,
                    WebGL = false
                };
            }

            _logger.LogInformation("🔧 Config: UA={UA}, Resolution={Res}", 
                config.UserAgent?.Substring(0, Math.Min(50, config.UserAgent.Length)), 
                config.ScreenResolution);

            // Создаем профиль
            var profile = new BrowserProfile
            {
                UserId = userId,
                Name = name,
                Config = config,
                Status = ProfileStatus.Stopped,
                CreatedAt = DateTime.UtcNow,
                ContainerId = string.Empty,
                ServerNodeIp = string.Empty,
                Port = 0
            };

            _logger.LogInformation("💾 Adding profile to database");
            _context.BrowserProfiles.Add(profile);
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Profile created successfully: ID={ProfileId}, Name='{Name}'", 
                    profile.Id, profile.Name);
                return profile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to save profile to database");
                throw;
            }
        }
        catch (InvalidOperationException)
        {
            throw; // Пробрасываем лимит профилей
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error creating profile for user {UserId}", userId);
            throw;
        }
    }

    public async Task<BrowserProfile> UpdateProfileAsync(BrowserProfile profile)
    {
        _context.BrowserProfiles.Update(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    public async Task<bool> StartProfileAsync(int profileId, int userId)
    {
        _logger.LogInformation("▶️ Starting profile {ProfileId} for user {UserId}", profileId, userId);
        
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null)
        {
            _logger.LogWarning("❌ Profile {ProfileId} not found", profileId);
            return false;
        }

        if (profile.Status == ProfileStatus.Running)
        {
            _logger.LogWarning("⚠️ Profile {ProfileId} already running", profileId);
            return false;
        }

        // Select server node
        var node = await _loadBalancerService.SelectNodeAsync();
        if (node == null)
        {
            _logger.LogWarning("❌ No available nodes for profile {ProfileId}", profileId);
            return false;
        }

        _logger.LogInformation("🖥️ Selected node: {NodeIp}", node.IpAddress);

        profile.Status = ProfileStatus.Starting;
        await _context.SaveChangesAsync();

        try
        {
            _logger.LogInformation("🐳 Creating Docker container for profile {ProfileId}", profileId);
            
            var containerId = await _dockerService.CreateBrowserContainerAsync(
                profileId,
                profile.Config,
                node.IpAddress
            );

            _logger.LogInformation("✅ Container created: {ContainerId}", containerId);

            profile.ContainerId = containerId;
            profile.ServerNodeIp = node.IpAddress;
            profile.Status = ProfileStatus.Running;
            profile.LastStartedAt = DateTime.UtcNow;

            // Get available port
            profile.Port = await _dockerService.GetContainerPortAsync(containerId);
            _logger.LogInformation("🔌 Container port: {Port}", profile.Port);

            node.ActiveContainers++;
            await _context.SaveChangesAsync();

            // Publish events
            _rabbitMQ.Publish("container.started", new { ProfileId = profileId, ContainerId = containerId });

            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "ContainerStarted",
                ProfileId = profileId,
                ContainerId = containerId,
                NodeIp = node.IpAddress,
                Timestamp = DateTime.UtcNow
            });

            await _kafkaService.PublishContainerLogAsync(containerId, 
                $"Container {containerId} started for profile {profileId} on node {node.IpAddress}");

            _logger.LogInformation("✅ Profile {ProfileId} started successfully on {NodeIp}:{Port}", 
                profileId, node.IpAddress, profile.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to start profile {ProfileId}", profileId);
            profile.Status = ProfileStatus.Error;
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> StopProfileAsync(int profileId, int userId)
    {
        _logger.LogInformation("⏹️ Stopping profile {ProfileId} for user {UserId}", profileId, userId);
        
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null || string.IsNullOrEmpty(profile.ContainerId))
        {
            _logger.LogWarning("❌ Profile {ProfileId} not found or not running", profileId);
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

            // Publish events
            _rabbitMQ.Publish("container.stopped", new { ProfileId = profileId });

            await _kafkaService.PublishProfileEventAsync("profile-events", new
            {
                EventType = "ContainerStopped",
                ProfileId = profileId,
                ContainerId = containerId,
                Timestamp = DateTime.UtcNow
            });

            await _kafkaService.PublishContainerLogAsync(containerId, 
                $"Container {containerId} stopped for profile {profileId}");

            _logger.LogInformation("✅ Profile {ProfileId} stopped successfully", profileId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to stop profile {ProfileId}", profileId);
            profile.Status = ProfileStatus.Error;
            await _context.SaveChangesAsync();
            return false;
        }
    }

    public async Task<bool> DeleteProfileAsync(int profileId, int userId)
    {
        _logger.LogInformation("🗑️ Deleting profile {ProfileId} for user {UserId}", profileId, userId);
        
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null)
        {
            _logger.LogWarning("❌ Profile {ProfileId} not found", profileId);
            return false;
        }

        if (profile.Status == ProfileStatus.Running)
        {
            _logger.LogInformation("⏹️ Stopping running profile before deletion");
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
                _logger.LogError(ex, "⚠️ Failed to delete container {ContainerId}", profile.ContainerId);
            }
        }

        _context.BrowserProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("✅ Profile {ProfileId} deleted successfully", profileId);
        return true;
    }
}