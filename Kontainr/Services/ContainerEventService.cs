using Docker.DotNet;
using Docker.DotNet.Models;

namespace Kontainr.Services;

/// <summary>
/// Background service that monitors Docker events for container crashes/restarts
/// and surfaces them as alerts on the dashboard.
/// </summary>
public class ContainerEventService : BackgroundService
{
    private readonly DockerHostManager _hostManager;
    private readonly DockerServiceFactory _dockerFactory;
    private readonly WebhookService _webhook;
    private readonly ILogger<ContainerEventService> _logger;
    private readonly List<ContainerAlert> _alerts = [];
    private readonly object _lock = new();
    private const int MaxAlerts = 50;

    public event Action? OnAlert;

    public ContainerEventService(DockerHostManager hostManager, DockerServiceFactory dockerFactory,
        WebhookService webhook, ILogger<ContainerEventService> logger)
    {
        _hostManager = hostManager;
        _dockerFactory = dockerFactory;
        _webhook = webhook;
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
            foreach (var hostId in _hostManager.GetAllHostIds())
            {
                try
                {
                    var docker = _dockerFactory.GetService(hostId);
                    var hostConfig = _hostManager.GetHostConfig(hostId);
                    var containers = await docker.GetContainersAsync(true);

                    foreach (var c in containers)
                    {
                        var name = c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12];

                        if (c.State == "exited" && c.Status.Contains("Exited ("))
                        {
                            var exitMatch = System.Text.RegularExpressions.Regex.Match(c.Status, @"Exited \((\d+)\)");
                            var exitCode = exitMatch.Success ? int.Parse(exitMatch.Groups[1].Value) : -1;

                            if (exitCode != 0)
                            {
                                AddAlert("crash", name, $"Exited with code {exitCode}", c.ID, hostId, hostConfig.Name);
                            }
                        }

                        if (c.State == "restarting")
                        {
                            AddAlert("restarting", name, "Container is restart-looping", c.ID, hostId, hostConfig.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check container events on host {Host}", hostId);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private void AddAlert(string type, string containerName, string message, string containerId, string hostId, string hostName)
    {
        lock (_lock)
        {
            if (_alerts.Any(a => a.ContainerName == containerName && a.Type == type
                && a.HostId == hostId && a.Timestamp > DateTime.UtcNow.AddMinutes(-5)))
                return;

            var displayName = hostId == "local" ? containerName : $"{containerName} ({hostName})";

            _alerts.Insert(0, new ContainerAlert
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Type = type,
                ContainerName = containerName,
                ContainerId = containerId,
                HostId = hostId,
                HostName = hostName,
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            if (_alerts.Count > MaxAlerts)
                _alerts.RemoveAt(_alerts.Count - 1);

            OnAlert?.Invoke();

            _ = Task.Run(async () =>
            {
                try { await _webhook.SendAlertAsync(displayName, type, message); }
                catch { }
            });
        }
    }
}

public class ContainerAlert
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public string HostId { get; set; } = "local";
    public string HostName { get; set; } = "Local";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}
