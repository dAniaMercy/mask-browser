using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProfileService> _logger;
    private readonly IMetricsService _metricsService;

    public ProfileService(
        ApplicationDbContext context,
        DockerService dockerService,
        LoadBalancerService loadBalancerService,
        RabbitMQService rabbitMQ,
        KafkaService kafkaService,
        IConfiguration configuration,
        ILogger<ProfileService> logger,
        IMetricsService metricsService)
    {
        _context = context;
        _dockerService = dockerService;
        _loadBalancerService = loadBalancerService;
        _rabbitMQ = rabbitMQ;
        _kafkaService = kafkaService;
        _configuration = configuration;
        _logger = logger;
        _metricsService = metricsService;
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
            // Обновляем существующие Free подписки с лимитом 1 до 3
            else if (user.Subscription.Tier == SubscriptionTier.Free && user.Subscription.MaxProfiles < 3)
            {
                _logger.LogInformation("🔄 Updating Free subscription limit from {Old} to 3 for user {UserId}", 
                    user.Subscription.MaxProfiles, userId);
                user.Subscription.MaxProfiles = 3;
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Subscription limit updated");
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
                config = BrowserConfigValidator.GetDefaultConfig();
            }

            // Валидация конфигурации профиля
            var validationResult = BrowserConfigValidator.Validate(config);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("❌ Profile config validation failed: {Errors}", 
                    string.Join(", ", validationResult.Errors));
                
                // Метрика для неудачной валидации
                _metricsService.IncrementProfileValidationFailed();
                
                throw new ArgumentException($"Invalid profile configuration: {string.Join(", ", validationResult.Errors)}");
            }

            if (validationResult.Warnings.Any())
            {
                _logger.LogInformation("⚠️ Profile config warnings: {Warnings}", 
                    string.Join(", ", validationResult.Warnings));
            }

            _logger.LogInformation("🔧 Config: UA={UA}, Resolution={Res}, Timezone={TZ}, Language={Lang}, WebRTC={WebRTC}, Canvas={Canvas}, WebGL={WebGL}", 
                config.UserAgent?.Substring(0, Math.Min(50, config.UserAgent.Length)), 
                config.ScreenResolution,
                config.Timezone,
                config.Language,
                config.WebRTC,
                config.Canvas,
                config.WebGL);

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

    public async Task<StartProfileResult> StartProfileAsync(int profileId, int userId)
    {
        _logger.LogInformation("▶️ Starting profile {ProfileId} for user {UserId}", profileId, userId);
        
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null)
        {
            _logger.LogWarning("❌ Profile {ProfileId} not found for user {UserId}", profileId, userId);
            return new StartProfileResult 
            { 
                Success = false, 
                ErrorMessage = "Profile not found" 
            };
        }

        if (profile.Status == ProfileStatus.Running)
        {
            _logger.LogWarning("⚠️ Profile {ProfileId} already running", profileId);
            return new StartProfileResult 
            { 
                Success = false, 
                ErrorMessage = "Profile is already running",
                Profile = profile
            };
        }

        // Select server node - если нет нод, создаем локальную
        var node = await _loadBalancerService.SelectNodeAsync();
        if (node == null)
        {
            _logger.LogWarning("❌ No available nodes for profile {ProfileId}, creating local node", profileId);
            
            // Получаем IP сервера из конфигурации
            var serverIp = _configuration["ServerIP"] ?? "127.0.0.1";
            _logger.LogInformation("🔧 Server IP from config: {ServerIp}", serverIp);
            
            // Создаем локальную ноду автоматически
            await _loadBalancerService.RegisterNodeAsync("local-node", serverIp, 1000);
            _logger.LogInformation("✅ Registered local node: {ServerIp}", serverIp);
            
            // Обновляем LastHealthCheck чтобы нода сразу была доступна
            var registeredNode = await _context.ServerNodes
                .FirstOrDefaultAsync(n => n.IpAddress == serverIp);
            if (registeredNode != null)
            {
                registeredNode.LastHealthCheck = DateTime.UtcNow;
                registeredNode.IsHealthy = true;
                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Updated node health: {ServerIp}, LastCheck={LastCheck}", 
                    serverIp, registeredNode.LastHealthCheck);
                
                // Используем обновленную ноду напрямую, без повторной проверки
                node = registeredNode;
                _logger.LogInformation("✅ Using updated node: {ServerIp}", serverIp);
            }
            else
            {
                _logger.LogWarning("⚠️ Node {ServerIp} not found after registration", serverIp);
                
                // Пробуем снова выбрать ноду
                _logger.LogInformation("🔍 Attempting to select node again...");
                node = await _loadBalancerService.SelectNodeAsync();
            }
            
            if (node == null)
            {
                _logger.LogError("❌ Failed to create or select node for profile {ProfileId}. Checking all nodes...", profileId);
                
                // Диагностика: проверяем все ноды
                var allNodes = await _context.ServerNodes.ToListAsync();
                foreach (var n in allNodes)
                {
                    _logger.LogWarning("Node: {Ip}, Healthy: {Healthy}, LastCheck: {LastCheck}, Active: {Active}/{Max}", 
                        n.IpAddress, n.IsHealthy, n.LastHealthCheck, n.ActiveContainers, n.MaxContainers);
                }
                
                return new StartProfileResult 
                { 
                    Success = false, 
                    ErrorMessage = "No server nodes available. Please contact administrator." 
                };
            }
        }

        _logger.LogInformation("🖥️ Selected node: {NodeIp}", node.IpAddress);

        // Используем транзакцию для атомарности операций
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            profile.Status = ProfileStatus.Starting;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.LogInformation("📝 Profile status set to Starting");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "❌ Failed to update profile status to Starting");
            throw;
        }

        try
        {
            _logger.LogInformation("🐳 Creating Docker container for profile {ProfileId} on node {NodeIp}", profileId, node.IpAddress);
            
            var containerId = await _dockerService.CreateBrowserContainerAsync(
                profileId,
                profile.Config,
                node.IpAddress
            );

            _logger.LogInformation("✅ Container created: {ContainerId}", containerId);

            // Начинаем новую транзакцию для обновления профиля
            using var updateTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Перезагружаем профиль и ноду из БД для транзакции
                profile = await _context.BrowserProfiles.FindAsync(profileId);
                node = await _context.ServerNodes.FindAsync(node.Id);
                
                if (profile == null || node == null)
                {
                    throw new InvalidOperationException("Profile or node not found during update");
                }

                profile.ContainerId = containerId;
                profile.ServerNodeIp = node.IpAddress;
                profile.Status = ProfileStatus.Running;
                profile.LastStartedAt = DateTime.UtcNow;

                // Get available port
                profile.Port = await _dockerService.GetContainerPortAsync(containerId);
                _logger.LogInformation("🔌 Container port: {Port}", profile.Port);

                node.ActiveContainers++;
                await _context.SaveChangesAsync();
                await updateTransaction.CommitAsync();

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

            }
            catch (Exception ex)
            {
                await updateTransaction.RollbackAsync();
                _logger.LogError(ex, "❌ Failed to update profile in database after container creation");
                
                // Очищаем контейнер, если не удалось обновить БД
                try
                {
                    await _dockerService.DeleteContainerAsync(containerId);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "⚠️ Failed to cleanup container {ContainerId}", containerId);
                }
                throw;
            }

            // Publish events (неблокирующие)
            _ = Task.Run(() =>
            {
                try
                {
                    _rabbitMQ.Publish("container.started", new { ProfileId = profileId, ContainerId = containerId });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to publish RabbitMQ event");
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
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
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to publish Kafka event");
                }
            });

            _logger.LogInformation("✅ Profile {ProfileId} started successfully on {NodeIp}:{Port}", 
                profileId, node.IpAddress, profile.Port);
            return new StartProfileResult 
            { 
                Success = true, 
                Profile = profile 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "❌ Failed to start profile {ProfileId} for user {UserId} on node {NodeIp}. " +
                "ContainerId: {ContainerId}, ProfileStatus: {Status}, Config: {Config}. " +
                "Error: {Error}",
                profileId, 
                userId,
                node?.IpAddress ?? "unknown",
                profile?.ContainerId ?? "none",
                profile?.Status.ToString() ?? "unknown",
                profile != null ? System.Text.Json.JsonSerializer.Serialize(profile.Config) : "null",
                ex.Message);
            
            // Очищаем контейнер, если он был создан, но не запущен
            if (!string.IsNullOrEmpty(profile.ContainerId))
            {
                try
                {
                    _logger.LogInformation("🧹 Cleaning up failed container {ContainerId}", profile.ContainerId);
                    await _dockerService.DeleteContainerAsync(profile.ContainerId);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "⚠️ Failed to cleanup container {ContainerId}", profile.ContainerId);
                }
            }
            
            // Сбрасываем статус на Stopped в транзакции
            using var rollbackTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                profile = await _context.BrowserProfiles.FindAsync(profileId);
                if (profile != null)
                {
                    profile.Status = ProfileStatus.Stopped;
                    profile.ServerNodeIp = string.Empty;
                    profile.Port = 0;
                    profile.ContainerId = string.Empty;
                    await _context.SaveChangesAsync();
                }
                await rollbackTransaction.CommitAsync();
            }
            catch (Exception rollbackEx)
            {
                await rollbackTransaction.RollbackAsync();
                _logger.LogError(rollbackEx, "❌ Failed to rollback profile status");
            }
            
            return new StartProfileResult 
            { 
                Success = false, 
                ErrorMessage = $"Failed to start profile: {ex.Message}",
                Profile = profile
            };
        }
    }

    public async Task<bool> ResetProfileErrorAsync(int profileId, int userId)
    {
        _logger.LogInformation("🔄 Resetting error status for profile {ProfileId} for user {UserId}", profileId, userId);
        
        var profile = await GetProfileAsync(profileId, userId);
        if (profile == null)
        {
            _logger.LogWarning("❌ Profile {ProfileId} not found", profileId);
            return false;
        }

        if (profile.Status != ProfileStatus.Error)
        {
            _logger.LogWarning("⚠️ Profile {ProfileId} is not in Error status (current: {Status})", profileId, profile.Status);
            return false;
        }

        // Очищаем контейнер, если он есть
        if (!string.IsNullOrEmpty(profile.ContainerId))
        {
            try
            {
                _logger.LogInformation("🧹 Cleaning up container {ContainerId}", profile.ContainerId);
                await _dockerService.DeleteContainerAsync(profile.ContainerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to cleanup container {ContainerId}", profile.ContainerId);
            }
        }

        // Сбрасываем статус
        profile.Status = ProfileStatus.Stopped;
        profile.ContainerId = string.Empty;
        profile.ServerNodeIp = string.Empty;
        profile.Port = 0;
        await _context.SaveChangesAsync();

        _logger.LogInformation("✅ Profile {ProfileId} error status reset", profileId);
        return true;
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
            profile.ServerNodeIp = string.Empty;
            profile.Port = 0;
            await _context.SaveChangesAsync();

            // Publish events (неблокирующие)
            _ = Task.Run(() =>
            {
                try
                {
                    _rabbitMQ.Publish("container.stopped", new { ProfileId = profileId });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to publish RabbitMQ event for stopped profile {ProfileId}", profileId);
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    await _kafkaService.PublishProfileEventAsync("profile-events", new
                    {
                        EventType = "ContainerStopped",
                        ProfileId = profileId,
                        ContainerId = containerId,
                        Timestamp = DateTime.UtcNow
                    });

                    await _kafkaService.PublishContainerLogAsync(containerId, 
                        $"Container {containerId} stopped for profile {profileId}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to publish Kafka event for stopped profile {ProfileId}", profileId);
                }
            });

            _logger.LogInformation("✅ Profile {ProfileId} stopped successfully", profileId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to stop profile {ProfileId}: {Error}", profileId, ex.Message);
            
            // Пытаемся принудительно остановить и удалить контейнер
            if (!string.IsNullOrEmpty(profile.ContainerId))
            {
                try
                {
                    _logger.LogInformation("🧹 Force cleaning up container {ContainerId}", profile.ContainerId);
                    await _dockerService.DeleteContainerAsync(profile.ContainerId);
                    profile.ContainerId = string.Empty;
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "⚠️ Failed to cleanup container {ContainerId}", profile.ContainerId);
                }
            }
            
            // Устанавливаем статус Stopped вместо Error, чтобы можно было попробовать снова
            try
            {
                profile.Status = ProfileStatus.Stopped;
                profile.ServerNodeIp = string.Empty;
                profile.Port = 0;
                if (string.IsNullOrEmpty(profile.ContainerId))
                {
                    profile.ContainerId = string.Empty;
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "❌ Failed to update profile status after stop error");
            }
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

public class StartProfileResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public BrowserProfile? Profile { get; set; }
}