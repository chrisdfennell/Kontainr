using Kontainr.Services;

namespace Kontainr.Tests;

public class WebhookProviderTests
{
    [Theory]
    [InlineData("https://discord.com/api/webhooks/123/abc", WebhookProvider.Discord)]
    [InlineData("https://hooks.slack.com/services/T/B/x", WebhookProvider.Slack)]
    [InlineData("https://ntfy.sh/my-topic", WebhookProvider.Ntfy)]
    [InlineData("https://my-server.local/ntfy/alerts", WebhookProvider.Ntfy)]
    [InlineData("https://example.com/webhook", WebhookProvider.Generic)]
    public void DetectProvider_identifies_destination_from_url(string url, WebhookProvider expected)
    {
        Assert.Equal(expected, WebhookService.DetectProvider(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DetectProvider_defaults_to_generic_for_empty(string? url)
    {
        Assert.Equal(WebhookProvider.Generic, WebhookService.DetectProvider(url!));
    }

    [Fact]
    public void DetectProvider_trims_surrounding_whitespace()
    {
        Assert.Equal(WebhookProvider.Discord,
            WebhookService.DetectProvider("  https://discord.com/api/webhooks/1/2  "));
    }
}
