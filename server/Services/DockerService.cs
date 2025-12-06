using Docker.DotNet;
using Docker.DotNet.Models;
using MaskBrowser.Server.Models;
using System.Text.Json;
using System.Threading;

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
        try
        {
            var containerName = $"maskbrowser-profile-{profileId}";
            var randomPort = new Random().Next(10000, 65535);
            var randomVncPort = new Random().Next(10000, 65535);
            var imageName = _configuration["Docker:BrowserImage"] ?? "maskbrowser/browser:latest";
            var networkName = _configuration["Docker:NetworkName"] ?? "maskbrowser-network";

            _logger.LogInformation("🐳 Creating container for profile {ProfileId} with image {Image}", profileId, imageName);

            // Проверяем и создаем сеть, если её нет
            await EnsureNetworkExistsAsync(networkName);

            // Проверяем наличие образа
            bool imageExists = false;
            try
            {
                _logger.LogInformation("🔍 Checking for image {Image}...", imageName);
                
                // Получаем все образы и проверяем по имени
                var allImages = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters { All = true });
                imageExists = allImages.Any(img => 
                    img.RepoTags != null && img.RepoTags.Any(tag => tag == imageName));

                if (!imageExists)
                {
                    _logger.LogWarning("⚠️ Image {Image} not found locally. Checking if it can be pulled from registry...", imageName);
                    
                    // Пытаемся загрузить образ из реестра (если он там есть)
                    try
                    {
                        var imageParts = imageName.Split(':');
                        var fromImage = imageParts[0];
                        var tag = imageParts.Length > 1 ? imageParts[1] : "latest";
                        
                        _logger.LogInformation("📥 Attempting to pull image {Image}:{Tag}...", fromImage, tag);
                        
                        await _dockerClient.Images.CreateImageAsync(
                            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
                            new AuthConfig(),
                            new Progress<JSONMessage>(msg => 
                            {
                                if (!string.IsNullOrEmpty(msg.Status))
                                    _logger.LogInformation("Docker pull: {Status}", msg.Status);
                            })
                        );
                        
                        // Проверяем снова после pull
                        allImages = await _dockerClient.Images.ListImagesAsync(new ImagesListParameters { All = true });
                        imageExists = allImages.Any(img => 
                            img.RepoTags != null && img.RepoTags.Any(t => t == imageName));
                        
                        if (imageExists)
                        {
                            _logger.LogInformation("✅ Image {Image} pulled successfully", imageName);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Image {Image} pull completed but image not found", imageName);
                        }
                    }
                    catch (Exception pullEx)
                    {
                        _logger.LogError(pullEx, "❌ Failed to pull image {Image} from registry", imageName);
                        // Не бросаем исключение здесь, проверим при создании контейнера
                    }
                }
                else
                {
                    _logger.LogInformation("✅ Image {Image} found locally", imageName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking for image {Image}: {Error}", imageName, ex.Message);
                // Продолжаем попытку создания контейнера, возможно образ есть
            }

            // Если образ не найден, выбрасываем понятную ошибку ДО попытки создания контейнера
            if (!imageExists)
            {
                var errorMessage = $"Docker image '{imageName}' not found. " +
                    $"Please build it first:\n" +
                    $"  cd /opt/mask-browser/infra\n" +
                    $"  docker build -t {imageName} -f Dockerfile.browser .";
                
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Создаем bind mount для сохранения данных профиля на хосте
            // Данные будут сохраняться в /var/lib/maskbrowser/profiles/{profileId} на хосте
            var hostProfilePath = _configuration["Docker:ProfileDataPath"] ?? $"/var/lib/maskbrowser/profiles/{profileId}";
            var containerProfilePath = "/app/data/profile";
            
            _logger.LogInformation("💾 Profile data will be saved to: {HostPath} -> {ContainerPath}", hostProfilePath, containerProfilePath);
            
            var createParams = new CreateContainerParameters
            {
                Image = imageName,
                Name = containerName,
                ExposedPorts = new Dictionary<string, EmptyStruct>
                {
                    { "8080/tcp", new EmptyStruct() },
                    { "5900/tcp", new EmptyStruct() },
                    { "6080/tcp", new EmptyStruct() }
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
                        },
                        {
                            "6080/tcp",
                            new List<PortBinding>
                            {
                                new PortBinding
                                {
                                    HostIP = "0.0.0.0",
                                    HostPort = randomVncPort.ToString()
                                }
                            }
                        }
                    },
                    Binds = new List<string>
                    {
                        $"{hostProfilePath}:{containerProfilePath}"
                    },
                    Memory = 512 * 1024 * 1024, // 512MB
                    MemorySwap = 512 * 1024 * 1024,
                    NanoCPUs = 500_000_000, // 0.5 CPU
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                    NetworkMode = networkName
                },
                Env = new List<string>
                {
                    $"PROFILE_ID={profileId}",
                    $"CONFIG={JsonSerializer.Serialize(config)}",
                    $"NODE_IP={nodeIp}"
                }
            };

            CreateContainerResponse response;
            try
            {
                _logger.LogInformation("📦 Calling Docker API to create container...");
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)); // 2 минуты таймаут
                response = await _dockerClient.Containers.CreateContainerAsync(createParams, cts.Token);
                _logger.LogInformation("✅ Container created: {ContainerId} for profile {ProfileId}", response.ID, profileId);
            }
            catch (DockerApiException ex)
            {
                _logger.LogError(ex, "❌ Docker API error creating container: {StatusCode} - {Message}", ex.StatusCode, ex.ResponseBody);
                
                // Проверяем, если ошибка связана с отсутствием образа
                if (ex.ResponseBody?.Contains("No such image") == true || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var errorMessage = $"Docker image '{imageName}' not found. " +
                        $"Please build it first: docker build -t {imageName} -f infra/Dockerfile.browser infra/";
                    _logger.LogError(errorMessage);
                    throw new InvalidOperationException(errorMessage, ex);
                }
                
                throw new InvalidOperationException($"Failed to create Docker container: {ex.ResponseBody}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "❌ Timeout creating Docker container for profile {ProfileId}", profileId);
                throw new InvalidOperationException("Timeout creating Docker container", ex);
            }

            try
            {
                _logger.LogInformation("🚀 Starting container {ContainerId}...", response.ID);
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)); // 1 минута таймаут
                await _dockerClient.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), cts.Token);
                _logger.LogInformation("✅ Container started: {ContainerId}", response.ID);
            }
            catch (DockerApiException ex)
            {
                _logger.LogError(ex, "❌ Docker API error starting container: {StatusCode} - {Message}", ex.StatusCode, ex.ResponseBody);
                // Пытаемся удалить контейнер, если не удалось запустить
                try
                {
                    await _dockerClient.Containers.RemoveContainerAsync(response.ID, new ContainerRemoveParameters { Force = true });
                }
                catch { }
                throw new InvalidOperationException($"Failed to start Docker container: {ex.ResponseBody}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "❌ Timeout starting Docker container {ContainerId}", response.ID);
                // Пытаемся удалить контейнер
                try
                {
                    await _dockerClient.Containers.RemoveContainerAsync(response.ID, new ContainerRemoveParameters { Force = true });
                }
                catch { }
                throw new InvalidOperationException("Timeout starting Docker container", ex);
            }

            return response.ID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create browser container for profile {ProfileId}: {Error}", profileId, ex.Message);
            throw;
        }
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
        if (container.NetworkSettings?.Ports != null)
        {
            // Получаем порт 6080 (WebSocket для noVNC)
            if (container.NetworkSettings.Ports.TryGetValue("6080/tcp", out var webSocketBindings) &&
                webSocketBindings != null && webSocketBindings.Count > 0)
            {
                return int.Parse(webSocketBindings[0].HostPort);
            }
            // Fallback на порт 8080, если 6080 не найден
            if (container.NetworkSettings.Ports.TryGetValue("8080/tcp", out var portBindings) &&
                portBindings != null && portBindings.Count > 0)
            {
                return int.Parse(portBindings[0].HostPort);
            }
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

    private async Task EnsureNetworkExistsAsync(string networkName)
    {
        try
        {
            _logger.LogInformation("🔍 Checking if network '{NetworkName}' exists...", networkName);
            
            var networks = await _dockerClient.Networks.ListNetworksAsync();
            var networkExists = networks.Any(n => n.Name == networkName);

            if (!networkExists)
            {
                _logger.LogWarning("⚠️ Network '{NetworkName}' not found. Creating it...", networkName);
                
                var networkCreateParams = new NetworksCreateParameters
                {
                    Name = networkName,
                    Driver = "bridge",
                    EnableIPv6 = false,
                    Internal = false,
                    Attachable = true,
                    CheckDuplicate = true
                };

                var response = await _dockerClient.Networks.CreateNetworkAsync(networkCreateParams);
                _logger.LogInformation("✅ Network '{NetworkName}' created successfully with ID: {NetworkId}", networkName, response.ID);
            }
            else
            {
                _logger.LogInformation("✅ Network '{NetworkName}' already exists", networkName);
            }
        }
        catch (DockerApiException ex)
        {
            // Если сеть уже существует (409 Conflict), это нормально
            if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogInformation("✅ Network '{NetworkName}' already exists (detected by conflict)", networkName);
            }
            else
            {
                _logger.LogError(ex, "❌ Failed to create network '{NetworkName}': {Message}", networkName, ex.Message);
                throw new InvalidOperationException($"Failed to create network '{networkName}': {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Unexpected error checking/creating network '{NetworkName}'", networkName);
            throw new InvalidOperationException($"Failed to ensure network '{networkName}' exists: {ex.Message}", ex);
        }
    }
}
