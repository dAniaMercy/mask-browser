using System.Net.Http.Json;
using System.Text.Json;
using MaskBrowser.Server.Models;

namespace MaskBrowser.Server.Services;

public class CyberneticsApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CyberneticsApiService> _logger;

    public CyberneticsApiService(HttpClient httpClient, IConfiguration configuration, ILogger<CyberneticsApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ServerNode?> CreateServerNodeAsync(string region, string instanceType)
    {
        // TODO: Integrate with actual Cybernetics API
        // This is a placeholder for the actual implementation
        
        var apiKey = _configuration["Cybernetics:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Cybernetics API key not configured");
            return null;
        }

        try
        {
            var request = new
            {
                Region = region,
                InstanceType = instanceType,
                Image = "ubuntu-22.04",
                Tags = new { Project = "MaskBrowser", Role = "node" }
            };

            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var response = await _httpClient.PostAsJsonAsync("/api/v1/servers", request);

            if (response.IsSuccessStatusCode)
            {
                var server = await response.Content.ReadFromJsonAsync<CyberneticsServerResponse>();
                _logger.LogInformation("Server created via Cybernetics API: {ServerIp}", server?.IpAddress);
                
                return new ServerNode
                {
                    Name = server?.Name ?? $"cybernetics-{Guid.NewGuid()}",
                    IpAddress = server?.IpAddress ?? "",
                    MaxContainers = 1000,
                    IsHealthy = true,
                    LastHealthCheck = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create server via Cybernetics API");
        }

        return null;
    }
}

public class CyberneticsServerResponse
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

