using System.Text;
using System.Text.Json;

namespace Kontainr.Services;

public enum WebhookProvider { Generic, Discord, Slack, Ntfy }

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
        if (alertType == "log-pattern" && !config.OnLogPattern) return;

        try
        {
            var url = config.Url.Trim();

            switch (DetectProvider(url))
            {
                case WebhookProvider.Discord:
                    await SendDiscordAsync(url, containerName, alertType, message);
                    break;
                case WebhookProvider.Slack:
                    await SendSlackAsync(url, containerName, alertType, message);
                    break;
                case WebhookProvider.Ntfy:
                    await SendNtfyAsync(url, containerName, alertType, message);
                    break;
                default:
                    await SendGenericAsync(url, containerName, alertType, message);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send webhook for {Container}", containerName);
        }
    }

    /// <summary>Identifies the webhook destination from its URL so the right payload format is used.</summary>
    internal static WebhookProvider DetectProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return WebhookProvider.Generic;
        url = url.Trim();
        if (url.Contains("discord.com/api/webhooks")) return WebhookProvider.Discord;
        if (url.Contains("hooks.slack.com")) return WebhookProvider.Slack;
        if (url.Contains("ntfy.sh/") || url.Contains("/ntfy/")) return WebhookProvider.Ntfy;
        return WebhookProvider.Generic;
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

    private async Task SendNtfyAsync(string url, string container, string type, string message)
    {
        var priority = type == "crash" ? "high" : "default";
        var tags = type == "crash" ? "rotating_light,docker" : "warning,docker";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(message, Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("Title", $"Kontainr: {container}");
        request.Headers.Add("Priority", priority);
        request.Headers.Add("Tags", tags);
        await _http.SendAsync(request);
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
