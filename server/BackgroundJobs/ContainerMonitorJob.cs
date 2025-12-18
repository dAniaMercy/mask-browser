using MaskBrowser.Server.Services;
using MaskBrowser.Server.Infrastructure;
using MaskBrowser.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MaskBrowser.Server.BackgroundJobs;

public class ContainerMonitorJob : BackgroundService
{
    private readonly DockerService _dockerService;
    private readonly ILogger<ContainerMonitorJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ContainerMonitorJob(
        DockerService dockerService,
        ILogger<ContainerMonitorJob> logger,
        IServiceProvider serviceProvider)
    {
        _dockerService = dockerService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var containers = await _dockerService.GetRunningContainersAsync();
                _logger.LogDebug("Monitoring {Count} containers", containers.Count);

                using var scope = _serviceProvider.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                metricsService.SetActiveContainers(containers.Count);

                // Проверяем здоровье контейнеров и синхронизируем статусы с БД
                await CheckContainerHealthAsync(context, containers, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in container monitor job");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }

    private async Task CheckContainerHealthAsync(
        ApplicationDbContext context, 
        List<Docker.DotNet.Models.ContainerListResponse> runningContainers,
        CancellationToken cancellationToken)
    {
        try
        {
            // Получаем все профили со статусом Running
            var runningProfiles = await context.BrowserProfiles
                .Where(p => p.Status == ProfileStatus.Running && !string.IsNullOrEmpty(p.ContainerId))
                .ToListAsync(cancellationToken);

            var runningContainerIds = runningContainers
                .Select(c => c.ID)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Проверяем профили, у которых контейнеры не запущены
            foreach (var profile in runningProfiles)
            {
                if (!runningContainerIds.Contains(profile.ContainerId))
                {
                    _logger.LogWarning(
                        "⚠️ Container {ContainerId} for profile {ProfileId} is not running, updating status",
                        profile.ContainerId, profile.Id);

                    // Обновляем статус профиля
                    profile.Status = ProfileStatus.Error;

                    // Обновляем счетчик ноды
                    if (!string.IsNullOrEmpty(profile.ServerNodeIp))
                    {
                        var node = await context.ServerNodes
                            .FirstOrDefaultAsync(n => n.IpAddress == profile.ServerNodeIp, cancellationToken);
                        if (node != null)
                        {
                            node.ActiveContainers = Math.Max(0, node.ActiveContainers - 1);
                        }
                    }

                    await context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "✅ Updated profile {ProfileId} status to Error (container not running)",
                        profile.Id);
                }
            }

            // Проверяем контейнеры без соответствующих профилей (orphaned containers)
            var profileContainerIds = runningProfiles
                .Select(p => p.ContainerId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var orphanedContainers = runningContainers
                .Where(c => !profileContainerIds.Contains(c.ID))
                .ToList();

            if (orphanedContainers.Any())
            {
                _logger.LogWarning(
                    "⚠️ Found {Count} orphaned containers (no corresponding profile in DB)",
                    orphanedContainers.Count);
                
                foreach (var container in orphanedContainers)
                {
                    _logger.LogDebug("Orphaned container: {ContainerId} ({Name})", 
                        container.ID, 
                        string.Join(", ", container.Names ?? Array.Empty<string>()));
                }
            }

            // Обновляем метрики
            var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
            metricsService.SetOrphanedContainers(orphanedContainers.Count);
            
            // Подсчитываем нездоровые контейнеры
            var unhealthyCount = 0;
            foreach (var profile in runningProfiles)
            {
                if (!string.IsNullOrEmpty(profile.ContainerId))
                {
                    var isHealthy = await _dockerService.IsContainerHealthyAsync(profile.ContainerId);
                    if (!isHealthy)
                    {
                        unhealthyCount++;
                    }
                }
            }
            metricsService.SetUnhealthyContainers(unhealthyCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking container health");
        }
    }
}

