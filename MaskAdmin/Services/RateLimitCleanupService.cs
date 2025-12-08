using MaskAdmin.Middleware;

namespace MaskAdmin.Services;

public class RateLimitCleanupService : BackgroundService
{
    private readonly ILogger<RateLimitCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);

    public RateLimitCleanupService(ILogger<RateLimitCleanupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rate Limit Cleanup Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                await RateLimitingMiddleware.CleanupOldEntries();
                _logger.LogDebug("Rate limit entries cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rate limit cleanup");
            }
        }

        _logger.LogInformation("Rate Limit Cleanup Service stopped");
    }
}
