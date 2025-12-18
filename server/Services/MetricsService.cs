using Prometheus;

namespace MaskBrowser.Server.Services;

public interface IMetricsService
{
    void IncrementContainerCreated();
    void IncrementContainerStopped();
    void SetActiveContainers(int count);
    void SetActiveUsers(int count);
    void IncrementContainerCreationFailed();
    void IncrementProfileValidationFailed();
    void IncrementContainerHealthCheck(bool isHealthy);
    void RecordContainerCreationDuration(double seconds);
    void IncrementDockerRetryAttempts(string operation);
    void SetUnhealthyContainers(int count);
    void SetOrphanedContainers(int count);
}

public class MetricsService : IMetricsService
{
    private static readonly Counter ContainerCreatedCounter = Metrics
        .CreateCounter("maskbrowser_containers_created_total", "Total containers created");

    private static readonly Counter ContainerStoppedCounter = Metrics
        .CreateCounter("maskbrowser_containers_stopped_total", "Total containers stopped");

    private static readonly Gauge ActiveContainersGauge = Metrics
        .CreateGauge("maskbrowser_containers_active", "Number of active containers");

    private static readonly Gauge ActiveUsersGauge = Metrics
        .CreateGauge("maskbrowser_users_active", "Number of active users");

    private static readonly Counter ContainerCreationFailedCounter = Metrics
        .CreateCounter("maskbrowser_containers_creation_failed_total", "Total container creation failures");

    private static readonly Counter ProfileValidationFailedCounter = Metrics
        .CreateCounter("maskbrowser_profile_validation_failed_total", "Total profile validation failures");

    private static readonly Counter ContainerHealthCheckCounter = Metrics
        .CreateCounter("maskbrowser_container_health_checks_total", "Total container health checks", new[] { "status" });

    private static readonly Histogram ContainerCreationDurationHistogram = Metrics
        .CreateHistogram("maskbrowser_container_creation_seconds", "Container creation duration in seconds",
            new HistogramConfiguration
            {
                Buckets = new[] { 0.5, 1.0, 2.0, 5.0, 10.0, 30.0, 60.0 }
            });

    private static readonly Counter DockerRetryAttemptsCounter = Metrics
        .CreateCounter("maskbrowser_docker_retry_attempts_total", "Total Docker operation retry attempts", new[] { "operation" });

    private static readonly Gauge UnhealthyContainersGauge = Metrics
        .CreateGauge("maskbrowser_containers_unhealthy", "Number of unhealthy containers");

    private static readonly Gauge OrphanedContainersGauge = Metrics
        .CreateGauge("maskbrowser_containers_orphaned", "Number of orphaned containers (no profile)");

    public void IncrementContainerCreated()
    {
        ContainerCreatedCounter.Inc();
    }

    public void IncrementContainerStopped()
    {
        ContainerStoppedCounter.Inc();
    }

    public void SetActiveContainers(int count)
    {
        ActiveContainersGauge.Set(count);
    }

    public void SetActiveUsers(int count)
    {
        ActiveUsersGauge.Set(count);
    }

    public void IncrementContainerCreationFailed()
    {
        ContainerCreationFailedCounter.Inc();
    }

    public void IncrementProfileValidationFailed()
    {
        ProfileValidationFailedCounter.Inc();
    }

    public void IncrementContainerHealthCheck(bool isHealthy)
    {
        ContainerHealthCheckCounter.WithLabels(isHealthy ? "healthy" : "unhealthy").Inc();
    }

    public void RecordContainerCreationDuration(double seconds)
    {
        ContainerCreationDurationHistogram.Observe(seconds);
    }

    public void IncrementDockerRetryAttempts(string operation)
    {
        DockerRetryAttemptsCounter.WithLabels(operation).Inc();
    }

    public void SetUnhealthyContainers(int count)
    {
        UnhealthyContainersGauge.Set(count);
    }

    public void SetOrphanedContainers(int count)
    {
        OrphanedContainersGauge.Set(count);
    }
}

