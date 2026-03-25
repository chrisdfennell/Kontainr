using System.Text;
using System.Text.Json;

namespace Kontainr.Services;

public class WebhookService
{
    private readonly SshSettingsService _settings;
    private readonly HttpClient _http;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(SshSettingsService settings, ILogger<WebhookService> logger)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _logger = logger;
    }

    public async Task SendAlertAsync(string containerName, string alertType, string message)
    {
        var config = _settings.GetWebhook();
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.Url)) return;
        if (alertType == "crash" && !config.OnCrash) return;
        if (alertType == "restarting" && !config.OnRestartLoop) return;

        try
        {
            var url = config.Url.Trim();

            // Detect Discord webhook
            if (url.Contains("discord.com/api/webhooks"))
            {
                await SendDiscordAsync(url, containerName, alertType, message);
            }
            // Detect Slack webhook
            else if (url.Contains("hooks.slack.com"))
            {
                await SendSlackAsync(url, containerName, alertType, message);
            }
            // Generic JSON POST
            else
            {
                await SendGenericAsync(url, containerName, alertType, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send webhook for {Container}", containerName);
        }
    }

    private async Task SendDiscordAsync(string url, string container, string type, string message)
    {
        var color = type == "crash" ? 0xF85149 : 0xD29922;
        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = $"Kontainr Alert: {container}",
                    description = message,
                    color,
                    fields = new[]
                    {
                        new { name = "Container", value = container, inline = true },
                        new { name = "Type", value = type, inline = true }
                    },
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private async Task SendSlackAsync(string url, string container, string type, string message)
    {
        var emoji = type == "crash" ? ":red_circle:" : ":warning:";
        var payload = new
        {
            text = $"{emoji} *Kontainr Alert*: `{container}` — {message}"
        };

        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private async Task SendGenericAsync(string url, string container, string type, string message)
    {
        var payload = new
        {
            source = "kontainr",
            container,
            alertType = type,
            message,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(payload);
        await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
    }
}
