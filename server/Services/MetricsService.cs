using Prometheus;

namespace MaskBrowser.Server.Services;

public interface IMetricsService
{
    void IncrementContainerCreated();
    void IncrementContainerStopped();
    void SetActiveContainers(int count);
    void SetActiveUsers(int count);
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
}

