namespace Kontainr.Services;

/// <summary>
/// Background service that checks for scheduled container restarts every minute.
/// Uses a simple cron-like matching: "HH:mm" daily, or "ddd HH:mm" weekly.
/// </summary>
public class ScheduledRestartService : BackgroundService
{
    private readonly DockerService _docker;
    private readonly SshSettingsService _settings;
    private readonly ToastService _toast;
    private readonly ILogger<ScheduledRestartService> _logger;
    private readonly HashSet<string> _executedThisMinute = [];

    public ScheduledRestartService(DockerService docker, SshSettingsService settings,
        ToastService toast, ILogger<ScheduledRestartService> logger)
    {
        _docker = docker;
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

                        try
                        {
                            var containers = await _docker.GetContainersAsync(true);
                            var match = containers.FirstOrDefault(c =>
                                c.Names.Any(n => n.TrimStart('/').Equals(sr.ContainerName, StringComparison.OrdinalIgnoreCase)));

                            if (match is not null && match.State == "running")
                            {
                                await _docker.RestartContainerAsync(match.ID);
                                _logger.LogInformation("Restarted {Container} on schedule", sr.ContainerName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Scheduled restart failed for {Container}", sr.ContainerName);
                        }
                    }
                }

                // Clear executed set when minute changes
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

    /// <summary>
    /// Simple cron matching:
    /// - "03:00" = daily at 3:00 AM
    /// - "Sun 03:00" = every Sunday at 3:00 AM
    /// - "Mon,Wed,Fri 06:00" = Mon/Wed/Fri at 6:00 AM
    /// - "*/6" = every 6 hours (at :00)
    /// </summary>
    private static bool ShouldRun(string expr, DateTime now)
    {
        expr = expr.Trim();

        // Every N hours: "*/6"
        if (expr.StartsWith("*/"))
        {
            if (int.TryParse(expr[2..], out var hours))
                return now.Minute == 0 && now.Hour % hours == 0;
            return false;
        }

        // Split day and time
        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string timePart;
        string? dayPart = null;

        if (parts.Length == 2)
        {
            dayPart = parts[0];
            timePart = parts[1];
        }
        else
        {
            timePart = parts[0];
        }

        // Parse time
        var timeParts = timePart.Split(':');
        if (timeParts.Length != 2) return false;
        if (!int.TryParse(timeParts[0], out var hour) || !int.TryParse(timeParts[1], out var minute))
            return false;

        if (now.Hour != hour || now.Minute != minute) return false;

        // Check day if specified
        if (dayPart is not null)
        {
            var days = dayPart.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var todayShort = now.DayOfWeek.ToString()[..3];
            return days.Any(d => d.Equals(todayShort, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }
}
