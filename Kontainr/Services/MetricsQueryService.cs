using Kontainr.Data;
using Microsoft.EntityFrameworkCore;

namespace Kontainr.Services;

public enum MetricsTimeRange { LastHour, LastDay, LastWeek }

public class MetricsQueryService
{
    private readonly IDbContextFactory<MetricsDbContext> _dbFactory;

    public MetricsQueryService(IDbContextFactory<MetricsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<MetricsDataPoint>> GetMetricsAsync(string hostId, string containerId, MetricsTimeRange range)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var (cutoff, bucketSeconds) = range switch
        {
            MetricsTimeRange.LastHour => (DateTime.UtcNow.AddHours(-1), 0),        // raw points
            MetricsTimeRange.LastDay => (DateTime.UtcNow.AddDays(-1), 300),         // 5 min buckets
            MetricsTimeRange.LastWeek => (DateTime.UtcNow.AddDays(-7), 1800),       // 30 min buckets
            _ => (DateTime.UtcNow.AddHours(-1), 0)
        };

        var query = db.ContainerMetrics
            .Where(m => m.HostId == hostId && m.ContainerId == containerId && m.Timestamp >= cutoff)
            .OrderBy(m => m.Timestamp);

        if (bucketSeconds == 0)
        {
            // Return raw points
            return await query.Select(m => new MetricsDataPoint
            {
                Timestamp = m.Timestamp,
                CpuPercent = m.CpuPercent,
                MemoryBytes = m.MemoryBytes,
                NetworkRxBytes = m.NetworkRxBytes,
                NetworkTxBytes = m.NetworkTxBytes
            }).ToListAsync();
        }

        // Downsample by grouping into time buckets
        var raw = await query.ToListAsync();
        return raw
            .GroupBy(m => new DateTime(m.Timestamp.Ticks / (TimeSpan.TicksPerSecond * bucketSeconds) * (TimeSpan.TicksPerSecond * bucketSeconds), DateTimeKind.Utc))
            .Select(g => new MetricsDataPoint
            {
                Timestamp = g.Key,
                CpuPercent = Math.Round(g.Average(m => m.CpuPercent), 2),
                MemoryBytes = (long)g.Average(m => m.MemoryBytes),
                NetworkRxBytes = (long)g.Average(m => m.NetworkRxBytes),
                NetworkTxBytes = (long)g.Average(m => m.NetworkTxBytes)
            })
            .ToList();
    }

    public async Task<AggregateMetrics?> GetAggregateAsync(string hostId, string containerId, MetricsTimeRange range)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cutoff = range switch
        {
            MetricsTimeRange.LastHour => DateTime.UtcNow.AddHours(-1),
            MetricsTimeRange.LastDay => DateTime.UtcNow.AddDays(-1),
            MetricsTimeRange.LastWeek => DateTime.UtcNow.AddDays(-7),
            _ => DateTime.UtcNow.AddHours(-1)
        };

        var metrics = await db.ContainerMetrics
            .Where(m => m.HostId == hostId && m.ContainerId == containerId && m.Timestamp >= cutoff)
            .ToListAsync();

        if (metrics.Count == 0) return null;

        return new AggregateMetrics
        {
            AvgCpu = Math.Round(metrics.Average(m => m.CpuPercent), 2),
            MaxCpu = Math.Round(metrics.Max(m => m.CpuPercent), 2),
            AvgMemory = (long)metrics.Average(m => m.MemoryBytes),
            PeakMemory = metrics.Max(m => m.MemoryBytes),
            TotalNetworkRx = metrics.Count > 0 ? metrics.Max(m => m.NetworkRxBytes) - metrics.Min(m => m.NetworkRxBytes) : 0,
            TotalNetworkTx = metrics.Count > 0 ? metrics.Max(m => m.NetworkTxBytes) - metrics.Min(m => m.NetworkTxBytes) : 0
        };
    }
}

public class MetricsDataPoint
{
    public DateTime Timestamp { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
}

public class AggregateMetrics
{
    public double AvgCpu { get; set; }
    public double MaxCpu { get; set; }
    public long AvgMemory { get; set; }
    public long PeakMemory { get; set; }
    public long TotalNetworkRx { get; set; }
    public long TotalNetworkTx { get; set; }
}
