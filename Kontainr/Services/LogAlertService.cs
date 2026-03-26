using System.Text.RegularExpressions;

namespace Kontainr.Services;

/// <summary>
/// Background service that monitors container logs for user-defined patterns
/// and sends webhook alerts when matches are found.
/// </summary>
public class LogAlertService : BackgroundService
{
    private readonly DockerHostManager _hostManager;
    private readonly DockerServiceFactory _dockerFactory;
    private readonly WebhookService _webhook;
    private readonly SshSettingsService _settings;
    private readonly ILogger<LogAlertService> _logger;

    private readonly Dictionary<string, string> _lastSeenLog = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, DateTime> _alertCooldowns = new();

    public LogAlertService(DockerHostManager hostManager, DockerServiceFactory dockerFactory,
        WebhookService webhook, SshSettingsService settings, ILogger<LogAlertService> logger)
    {
        _hostManager = hostManager;
        _dockerFactory = dockerFactory;
        _webhook = webhook;
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
                var rules = _settings.GetLogAlertRules().Where(r => r.Enabled).ToList();
                if (rules.Count > 0)
                {
                    foreach (var hostId in _hostManager.GetAllHostIds())
                    {
                        try
                        {
                            var docker = _dockerFactory.GetService(hostId);
                            var containers = await docker.GetContainersAsync(false);

                            foreach (var rule in rules)
                            {
                                var matching = containers
                                    .Where(c => c.Names.Any(n => n.TrimStart('/').Equals(rule.ContainerName, StringComparison.OrdinalIgnoreCase)))
                                    .ToList();

                                foreach (var container in matching)
                                {
                                    await CheckLogsForPattern(docker, container.ID, rule, hostId, ct);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Log alert check failed on host {Host}", hostId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Log alert check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task CheckLogsForPattern(DockerService docker, string containerId, Models.LogAlertRule rule, string hostId, CancellationToken ct)
    {
        try
        {
            var logs = await docker.GetContainerLogsAsync(containerId, 50);
            if (string.IsNullOrEmpty(logs)) return;

            var logKey = $"{hostId}:{containerId}:{rule.Id}";
            lock (_lock)
            {
                if (_lastSeenLog.TryGetValue(logKey, out var lastHash) && lastHash == logs.GetHashCode().ToString())
                    return;
                _lastSeenLog[logKey] = logs.GetHashCode().ToString();
            }

            var cooldownKey = $"{hostId}:{containerId}:{rule.Pattern}";
            lock (_lock)
            {
                if (_alertCooldowns.TryGetValue(cooldownKey, out var lastAlert) && lastAlert > DateTime.UtcNow.AddMinutes(-5))
                    return;
            }

            var lines = logs.Split('\n');
            foreach (var line in lines)
            {
                bool matched;
                try
                {
                    matched = Regex.IsMatch(line, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                }
                catch
                {
                    matched = line.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
                }

                if (matched)
                {
                    lock (_lock)
                    {
                        _alertCooldowns[cooldownKey] = DateTime.UtcNow;
                    }

                    var trimmedLine = line.Length > 200 ? line[..200] + "..." : line;
                    await _webhook.SendAlertAsync(rule.ContainerName, "log-pattern",
                        $"Pattern \"{rule.Pattern}\" matched: {trimmedLine}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check logs for {Container} on {Host}", rule.ContainerName, hostId);
        }
    }
}
