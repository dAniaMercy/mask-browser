using Microsoft.EntityFrameworkCore;
using MaskBrowser.Server.Infrastructure;

namespace MaskBrowser.Server.BackgroundJobs;

public class SessionCleanupJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupJob> _logger;

    public SessionCleanupJob(IServiceProvider serviceProvider, ILogger<SessionCleanupJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Clean up expired sessions, old logs, etc.
                // This is a placeholder for actual cleanup logic

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in session cleanup job");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}

