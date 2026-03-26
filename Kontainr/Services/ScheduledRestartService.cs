namespace Kontainr.Services;

/// <summary>
/// Background service that checks for scheduled container restarts every minute.
/// Uses a simple cron-like matching: "HH:mm" daily, or "ddd HH:mm" weekly.
/// </summary>
public class ScheduledRestartService : BackgroundService
{
    private readonly DockerHostManager _hostManager;
    private readonly DockerServiceFactory _dockerFactory;
    private readonly SshSettingsService _settings;
    private readonly ToastService _toast;
    private readonly ILogger<ScheduledRestartService> _logger;
    private readonly HashSet<string> _executedThisMinute = [];

    public ScheduledRestartService(DockerHostManager hostManager, DockerServiceFactory dockerFactory,
        SshSettingsService settings, ToastService toast, ILogger<ScheduledRestartService> logger)
    {
        _hostManager = hostManager;
        _dockerFactory = dockerFactory;
        _settings = settings;
        _toast = toast;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(5000, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var schedules = _settings.GetScheduledRestarts();

                foreach (var sr in schedules.Where(s => s.Enabled))
                {
                    if (ShouldRun(sr.CronExpression, now) && !_executedThisMinute.Contains(sr.Id))
                    {
                        _executedThisMinute.Add(sr.Id);
                        _logger.LogInformation("Scheduled restart: {Container}", sr.ContainerName);

                        // Try each host to find the container
                        foreach (var hostId in _hostManager.GetAllHostIds())
                        {
                            try
                            {
                                var docker = _dockerFactory.GetService(hostId);
                                var containers = await docker.GetContainersAsync(true);
                                var match = containers.FirstOrDefault(c =>
                                    c.Names.Any(n => n.TrimStart('/').Equals(sr.ContainerName, StringComparison.OrdinalIgnoreCase)));

                                if (match is not null && match.State == "running")
                                {
                                    await docker.RestartContainerAsync(match.ID);
                                    _logger.LogInformation("Restarted {Container} on host {Host}", sr.ContainerName, hostId);
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Scheduled restart failed for {Container} on {Host}", sr.ContainerName, hostId);
                            }
                        }
                    }
                }

                if (now.Second < 5)
                    _executedThisMinute.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scheduled restart check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private static bool ShouldRun(string expr, DateTime now)
    {
        expr = expr.Trim();

        if (expr.StartsWith("*/"))
        {
            if (int.TryParse(expr[2..], out var hours))
                return now.Minute == 0 && now.Hour % hours == 0;
            return false;
        }

        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string timePart;
        string? dayPart = null;

        if (parts.Length == 2) { dayPart = parts[0]; timePart = parts[1]; }
        else { timePart = parts[0]; }

        var timeParts = timePart.Split(':');
        if (timeParts.Length != 2) return false;
        if (!int.TryParse(timeParts[0], out var hour) || !int.TryParse(timeParts[1], out var minute))
            return false;

        if (now.Hour != hour || now.Minute != minute) return false;

        if (dayPart is not null)
        {
            var days = dayPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var todayShort = now.DayOfWeek.ToString()[..3];
            return days.Any(d => d.Equals(todayShort, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }
}
