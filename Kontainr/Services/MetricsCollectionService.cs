using Docker.DotNet.Models;
using Kontainr.Data;
using Microsoft.EntityFrameworkCore;

namespace Kontainr.Services;

public class MetricsCollectionService : BackgroundService
{
    private readonly DockerHostManager _hostManager;
    private readonly DockerServiceFactory _dockerFactory;
    private readonly IDbContextFactory<MetricsDbContext> _dbFactory;
    private readonly StatsHistoryService _statsHistory;
    private readonly SshSettingsService _settings;
    private readonly ILogger<MetricsCollectionService> _logger;
    private DateTime _lastPrune = DateTime.MinValue;

    public MetricsCollectionService(
        DockerHostManager hostManager,
        DockerServiceFactory dockerFactory,
        IDbContextFactory<MetricsDbContext> dbFactory,
        StatsHistoryService statsHistory,
        SshSettingsService settings,
        ILogger<MetricsCollectionService> logger)
    {
        _hostManager = hostManager;
        _dockerFactory = dockerFactory;
        _dbFactory = dbFactory;
        _statsHistory = statsHistory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(5000, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CollectAllHostMetrics(ct);

                // Prune old metrics once per hour
                if (DateTime.UtcNow - _lastPrune > TimeSpan.FromHours(1))
                {
                    await PruneOldMetrics(ct);
                    _lastPrune = DateTime.UtcNow;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Metrics collection failed");
            }

            var interval = _settings.GetMetricsCollectionInterval();
            await Task.Delay(TimeSpan.FromSeconds(interval), ct);
        }
    }

    private async Task CollectAllHostMetrics(CancellationToken ct)
    {
        var metrics = new List<ContainerMetric>();

        foreach (var hostId in _hostManager.GetAllHostIds())
        {
            try
            {
                var docker = _dockerFactory.GetService(hostId);
                var containers = await docker.GetContainersAsync(false); // running only

                foreach (var container in containers)
                {
                    try
                    {
                        var stats = await docker.GetContainerStatsOnceAsync(container.ID);
                        if (stats is null) continue;

                        var (cpu, mem, netRx, netTx) = CalculateStats(stats);
                        var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? container.ID[..12];
                        var historyKey = hostId == "local" ? container.ID : $"{hostId}:{container.ID}";

                        // Feed live sparklines
                        _statsHistory.Record(historyKey, cpu, mem, netRx, netTx);

                        // Queue for DB persistence
                        metrics.Add(new ContainerMetric
                        {
                            HostId = hostId,
                            ContainerId = container.ID,
                            ContainerName = name,
                            Timestamp = DateTime.UtcNow,
                            CpuPercent = cpu,
                            MemoryBytes = mem,
                            NetworkRxBytes = netRx,
                            NetworkTxBytes = netTx
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to collect stats for container {Id} on host {Host}",
                            container.ID[..12], hostId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to collect metrics from host {Host}", hostId);
            }
        }

        if (metrics.Count > 0)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            db.ContainerMetrics.AddRange(metrics);
            await db.SaveChangesAsync(ct);
        }
    }

    private static (double cpu, long mem, long netRx, long netTx) CalculateStats(ContainerStatsResponse stats)
    {
        // CPU calculation
        double cpu = 0;
        if (stats.CPUStats.CPUUsage.TotalUsage > 0 && stats.PreCPUStats.CPUUsage.TotalUsage > 0)
        {
            var cpuDelta = (double)(stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage);
            var sysDelta = (double)(stats.CPUStats.SystemUsage - stats.PreCPUStats.SystemUsage);
            var numCpus = stats.CPUStats.OnlineCPUs > 0 ? stats.CPUStats.OnlineCPUs : 1;
            if (sysDelta > 0)
                cpu = Math.Round(cpuDelta / sysDelta * numCpus * 100, 2);
        }

        // Memory
        var mem = (long)stats.MemoryStats.Usage;

        // Network I/O
        long netRx = 0, netTx = 0;
        if (stats.Networks is not null)
        {
            foreach (var net in stats.Networks.Values)
            {
                netRx += (long)net.RxBytes;
                netTx += (long)net.TxBytes;
            }
        }

        return (cpu, mem, netRx, netTx);
    }

    private async Task PruneOldMetrics(CancellationToken ct)
    {
        try
        {
            var retentionDays = _settings.GetMetricsRetentionDays();
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            await db.ContainerMetrics.Where(m => m.Timestamp < cutoff).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to prune old metrics");
        }
    }
}
