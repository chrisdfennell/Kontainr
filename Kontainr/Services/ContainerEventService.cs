using Docker.DotNet;
using Docker.DotNet.Models;

namespace Kontainr.Services;

/// <summary>
/// Background service that monitors Docker events for container crashes/restarts
/// and surfaces them as alerts on the dashboard.
/// </summary>
public class ContainerEventService : BackgroundService
{
    private readonly DockerService _docker;
    private readonly ILogger<ContainerEventService> _logger;
    private readonly List<ContainerAlert> _alerts = [];
    private readonly object _lock = new();
    private const int MaxAlerts = 50;

    public event Action? OnAlert;

    public ContainerEventService(DockerService docker, ILogger<ContainerEventService> logger)
    {
        _docker = docker;
        _logger = logger;
    }

    public List<ContainerAlert> GetAlerts(int count = 20)
    {
        lock (_lock) { return _alerts.Take(count).ToList(); }
    }

    public void DismissAlert(string id)
    {
        lock (_lock) { _alerts.RemoveAll(a => a.Id == id); }
    }

    public void DismissAll()
    {
        lock (_lock) { _alerts.Clear(); }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Give the app time to start
        await Task.Delay(3000, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Check all containers for unexpected restarts or crashes
                var containers = await _docker.GetContainersAsync(true);
                foreach (var c in containers)
                {
                    var name = c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12];

                    // Detect exited containers that have a restart policy (unexpected crash)
                    if (c.State == "exited" && c.Status.Contains("Exited ("))
                    {
                        // Extract exit code
                        var exitMatch = System.Text.RegularExpressions.Regex.Match(c.Status, @"Exited \((\d+)\)");
                        var exitCode = exitMatch.Success ? int.Parse(exitMatch.Groups[1].Value) : -1;

                        if (exitCode != 0) // Non-zero exit = crash
                        {
                            AddAlert("crash", name, $"Exited with code {exitCode}", c.ID);
                        }
                    }

                    // Detect containers in restarting state
                    if (c.State == "restarting")
                    {
                        AddAlert("restarting", name, "Container is restart-looping", c.ID);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check container events");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private void AddAlert(string type, string containerName, string message, string containerId)
    {
        lock (_lock)
        {
            // Don't duplicate — check if same container+type already alerted in last 5 min
            if (_alerts.Any(a => a.ContainerName == containerName && a.Type == type
                && a.Timestamp > DateTime.UtcNow.AddMinutes(-5)))
                return;

            _alerts.Insert(0, new ContainerAlert
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = type,
                ContainerName = containerName,
                ContainerId = containerId,
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            if (_alerts.Count > MaxAlerts)
                _alerts.RemoveAt(_alerts.Count - 1);

            OnAlert?.Invoke();
        }
    }
}

public class ContainerAlert
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
