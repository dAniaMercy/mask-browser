using System.Collections.Concurrent;

namespace MaskAdmin.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    // Store: IP -> (RequestTimes, IsBlocked, BlockedUntil)
    private static readonly ConcurrentDictionary<string, RateLimitInfo> _requestStore = new();

    // Configuration
    private const int MaxLoginAttempts = 5;
    private const int LoginWindowSeconds = 60;
    private const int BlockDurationMinutes = 15;

    private const int MaxApiRequests = 100;
    private const int ApiWindowSeconds = 60;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = GetClientIpAddress(context);
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Check if IP is blocked
        if (_requestStore.TryGetValue(ipAddress, out var info))
        {
            if (info.IsBlocked && info.BlockedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("Blocked request from {IP} until {BlockedUntil}", ipAddress, info.BlockedUntil);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = $"You have been temporarily blocked. Please try again after {(info.BlockedUntil - DateTime.UtcNow).TotalMinutes:F0} minutes.",
                    retryAfter = info.BlockedUntil
                });
                return;
            }

            // Unblock if time has passed
            if (info.IsBlocked && info.BlockedUntil <= DateTime.UtcNow)
            {
                _logger.LogInformation("Unblocking IP {IP}", ipAddress);
                _requestStore.TryRemove(ipAddress, out _);
            }
        }

        // Apply rate limiting based on endpoint
        if (path.Contains("/auth/login"))
        {
            if (!await CheckLoginRateLimit(ipAddress))
            {
                _logger.LogWarning("Login rate limit exceeded for IP {IP}", ipAddress);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too many login attempts",
                    message = $"Maximum {MaxLoginAttempts} login attempts allowed per {LoginWindowSeconds} seconds. You have been blocked for {BlockDurationMinutes} minutes.",
                    retryAfter = DateTime.UtcNow.AddMinutes(BlockDurationMinutes)
                });
                return;
            }
        }
        else if (path.Contains("/api/"))
        {
            if (!await CheckApiRateLimit(ipAddress))
            {
                _logger.LogWarning("API rate limit exceeded for IP {IP}", ipAddress);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Rate limit exceeded",
                    message = $"Maximum {MaxApiRequests} API requests allowed per {ApiWindowSeconds} seconds.",
                    retryAfter = DateTime.UtcNow.AddSeconds(ApiWindowSeconds)
                });
                return;
            }
        }

        await _next(context);
    }

    private async Task<bool> CheckLoginRateLimit(string ipAddress)
    {
        var now = DateTime.UtcNow;
        var info = _requestStore.GetOrAdd(ipAddress, _ => new RateLimitInfo());

        // Clean old requests
        info.LoginAttempts.RemoveAll(t => (now - t).TotalSeconds > LoginWindowSeconds);

        // Check if limit exceeded
        if (info.LoginAttempts.Count >= MaxLoginAttempts)
        {
            // Block the IP
            info.IsBlocked = true;
            info.BlockedUntil = now.AddMinutes(BlockDurationMinutes);
            _logger.LogWarning("IP {IP} blocked due to {Count} login attempts in {Window}s",
                ipAddress, info.LoginAttempts.Count, LoginWindowSeconds);
            return false;
        }

        // Add current attempt
        info.LoginAttempts.Add(now);
        return true;
    }

    private async Task<bool> CheckApiRateLimit(string ipAddress)
    {
        var now = DateTime.UtcNow;
        var info = _requestStore.GetOrAdd(ipAddress, _ => new RateLimitInfo());

        // Clean old requests
        info.ApiRequests.RemoveAll(t => (now - t).TotalSeconds > ApiWindowSeconds);

        // Check if limit exceeded
        if (info.ApiRequests.Count >= MaxApiRequests)
        {
            return false;
        }

        // Add current request
        info.ApiRequests.Add(now);
        return true;
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        // Check for real IP (Cloudflare, etc.)
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // Cleanup task to prevent memory leaks
    public static Task CleanupOldEntries()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var kvp in _requestStore)
        {
            var info = kvp.Value;

            // Remove if not blocked and no recent requests
            if (!info.IsBlocked &&
                info.LoginAttempts.Count == 0 &&
                info.ApiRequests.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }

            // Remove if blocked time has passed and no requests
            if (info.IsBlocked &&
                info.BlockedUntil < now.AddHours(-1) &&
                info.LoginAttempts.Count == 0 &&
                info.ApiRequests.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _requestStore.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

public class RateLimitInfo
{
    public List<DateTime> LoginAttempts { get; set; } = new();
    public List<DateTime> ApiRequests { get; set; } = new();
    public bool IsBlocked { get; set; }
    public DateTime BlockedUntil { get; set; }
}
