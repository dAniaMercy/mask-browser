using Docker.DotNet;
using Docker.DotNet.Models;
using MaskBrowser.Server.Models;
using System.Text.Json;

namespace MaskBrowser.Server.Services;

public class DockerService
{
    private readonly DockerClient _dockerClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DockerService> _logger;

    public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
{
    _configuration = configuration;
    _logger = logger;

    string? socketPath = _configuration["Docker:SocketPath"] ?? Environment.GetEnvironmentVariable("DOCKER_HOST");

    // 🧩 Автоопределение сокета
    if (string.IsNullOrEmpty(socketPath))
    {
        if (OperatingSystem.IsWindows())
            socketPath = "npipe://./pipe/docker_engine";
        else
            socketPath = "unix:///var/run/docker.sock";
    }

    // 🧹 Если путь не содержит схему — добавляем её
    if (!socketPath.Contains("://"))
        socketPath = $"unix://{socketPath}";

    // 🧹 Меняем ошибочную file:// на unix://
    if (socketPath.StartsWith("file://"))
        socketPath = socketPath.Replace("file://", "unix://");

    try
    {
        _logger.LogInformation("Connecting to Docker at {SocketPath}", socketPath);
        var dockerConfig = new DockerClientConfiguration(new Uri(socketPath));
        _dockerClient = dockerConfig.CreateClient();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize Docker client with path: {SocketPath}", socketPath);
        throw;
    }
}

    public async Task<string> CreateBrowserContainerAsync(int profileId, BrowserConfig config, string nodeIp)
    {
        var containerName = $"maskbrowser-profile-{profileId}";
        var randomPort = new Random().Next(10000, 65535);

        var createParams = new CreateContainerParameters
        {
            Image = "maskbrowser/browser:latest",
            Name = containerName,
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                { "8080/tcp", new EmptyStruct() }
            },
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    {
                        "8080/tcp",
                        new List<PortBinding>
                        {
                            new PortBinding
                            {
                                HostIP = "0.0.0.0",
                                HostPort = randomPort.ToString()
                            }
                        }
                    }
                },
                Memory = 512 * 1024 * 1024, // 512MB
                MemorySwap = 512 * 1024 * 1024,
                NanoCPUs = 500_000_000, // 0.5 CPU
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                NetworkMode = _configuration["Docker:NetworkName"] ?? "maskbrowser-network"
            },
            Env = new List<string>
            {
                $"PROFILE_ID={profileId}",
                $"CONFIG={JsonSerializer.Serialize(config)}",
                $"NODE_IP={nodeIp}"
            }
        };

        var response = await _dockerClient.Containers.CreateContainerAsync(createParams);
        await _dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters());

        _logger.LogInformation("Container created: {ContainerId} for profile {ProfileId}", response.ID, profileId);
        return response.ID;
    }

    public async Task StopContainerAsync(string containerId)
    {
        await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
        _logger.LogInformation("Container stopped: {ContainerId}", containerId);
    }

    public async Task DeleteContainerAsync(string containerId)
    {
        try
        {
            await _dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
        }
        catch { }

        await _dockerClient.Containers.RemoveContainerAsync(
            containerId,
            new ContainerRemoveParameters { Force = true }
        );

        _logger.LogInformation("Container deleted: {ContainerId}", containerId);
    }

    public async Task<int> GetContainerPortAsync(string containerId)
    {
        var container = await _dockerClient.Containers.InspectContainerAsync(containerId);
        if (container.NetworkSettings?.Ports != null &&
            container.NetworkSettings.Ports.TryGetValue("8080/tcp", out var portBindings) &&
            portBindings != null && portBindings.Count > 0)
        {
            return int.Parse(portBindings[0].HostPort);
        }
        return 0;
    }

    public async Task<List<ContainerListResponse>> GetRunningContainersAsync()
    {
        var containers = await _dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters { All = false }
        );
        return containers.Where(c => c.Names.Any(n => n.Contains("maskbrowser-profile"))).ToList();
    }
}
