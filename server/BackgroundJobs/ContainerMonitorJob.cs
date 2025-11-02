using MaskBrowser.Server.Services;

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
                _logger.LogInformation("Monitoring {Count} containers", containers.Count);

                using var scope = _serviceProvider.CreateScope();
                var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
                metricsService.SetActiveContainers(containers.Count);

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in container monitor job");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}

