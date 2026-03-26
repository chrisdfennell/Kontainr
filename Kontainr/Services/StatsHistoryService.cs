namespace Kontainr.Services;

public class StatsHistoryService
{
    private readonly Dictionary<string, ContainerStatsHistory> _history = [];
    private const int MaxPoints = 60; // 5 minutes at 5s intervals

    public void Record(string containerId, double cpuPercent, long memoryBytes)
    {
        Record(containerId, cpuPercent, memoryBytes, 0, 0);
    }

    public void Record(string containerId, double cpuPercent, long memoryBytes, long networkRxBytes, long networkTxBytes)
    {
        if (!_history.TryGetValue(containerId, out var history))
        {
            history = new ContainerStatsHistory();
            _history[containerId] = history;
        }

        history.CpuPoints.Add(cpuPercent);
        history.MemPoints.Add(memoryBytes);
        history.NetRxPoints.Add(networkRxBytes);
        history.NetTxPoints.Add(networkTxBytes);

        if (history.CpuPoints.Count > MaxPoints) history.CpuPoints.RemoveAt(0);
        if (history.MemPoints.Count > MaxPoints) history.MemPoints.RemoveAt(0);
        if (history.NetRxPoints.Count > MaxPoints) history.NetRxPoints.RemoveAt(0);
        if (history.NetTxPoints.Count > MaxPoints) history.NetTxPoints.RemoveAt(0);
    }

    public ContainerStatsHistory? GetHistory(string containerId)
    {
        return _history.GetValueOrDefault(containerId);
    }

    public void Remove(string containerId)
    {
        _history.Remove(containerId);
    }
}

public class ContainerStatsHistory
{
    public List<double> CpuPoints { get; set; } = [];
    public List<long> MemPoints { get; set; } = [];
    public List<long> NetRxPoints { get; set; } = [];
    public List<long> NetTxPoints { get; set; } = [];
}
