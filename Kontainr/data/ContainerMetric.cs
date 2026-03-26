namespace Kontainr.Data;

public class ContainerMetric
{
    public long Id { get; set; }
    public string HostId { get; set; } = "local";
    public string ContainerId { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryBytes { get; set; }
    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
}
