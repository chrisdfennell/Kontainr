using System.Text.RegularExpressions;

namespace Kontainr.Services;

/// <summary>
/// Background service that monitors container logs for user-defined patterns
/// and sends webhook alerts when matches are found.
/// </summary>
public class LogAlertService : BackgroundService
{
    private readonly DockerService _docker;
    private readonly WebhookService _webhook;
    private readonly SshSettingsService _settings;
    private readonly ILogger<LogAlertService> _logger;

    // Track last-seen log position per container to avoid duplicate alerts
    private readonly Dictionary<string, string> _lastSeenLog = new();
    private readonly object _lock = new();

    // Dedup: don't re-alert same container+pattern within 5 minutes
    private readonly Dictionary<string, DateTime> _alertCooldowns = new();

    public LogAlertService(DockerService docker, WebhookService webhook,
        SshSettingsService settings, ILogger<LogAlertService> logger)
    {
        _docker = docker;
        _webhook = webhook;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(5000, ct); // Let app start up

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var rules = _settings.GetLogAlertRules().Where(r => r.Enabled).ToList();
                if (rules.Count > 0)
                {
                    var containers = await _docker.GetContainersAsync(false); // running only
                    foreach (var rule in rules)
                    {
                        var matching = containers
                            .Where(c => c.Names.Any(n => n.TrimStart('/').Equals(rule.ContainerName, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        foreach (var container in matching)
                        {
                            await CheckLogsForPattern(container.ID, rule, ct);
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

    private async Task CheckLogsForPattern(string containerId, Models.LogAlertRule rule, CancellationToken ct)
    {
        try
        {
            var logs = await _docker.GetContainerLogsAsync(containerId, 50);
            if (string.IsNullOrEmpty(logs)) return;

            // Check if we've already scanned these exact logs
            var logKey = $"{containerId}:{rule.Id}";
            lock (_lock)
            {
                if (_lastSeenLog.TryGetValue(logKey, out var lastHash) && lastHash == logs.GetHashCode().ToString())
                    return;
                _lastSeenLog[logKey] = logs.GetHashCode().ToString();
            }

            // Check cooldown
            var cooldownKey = $"{containerId}:{rule.Pattern}";
            lock (_lock)
            {
                if (_alertCooldowns.TryGetValue(cooldownKey, out var lastAlert) && lastAlert > DateTime.UtcNow.AddMinutes(-5))
                    return;
            }

            // Search for pattern
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
                    // Invalid regex, fall back to simple contains
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
                    break; // One alert per check cycle per rule
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check logs for {Container}", rule.ContainerName);
        }
    }
}
