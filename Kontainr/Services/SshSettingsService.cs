using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Kontainr.Models;

namespace Kontainr.Services;

public class SshSettingsService
{
    private readonly IDataProtector _protector;
    private readonly string _filePath;
    private readonly object _lock = new();
    private AppSettings? _cache;

    public SshSettingsService(IDataProtectionProvider dpProvider, IWebHostEnvironment env, IConfiguration config)
    {
        _protector = dpProvider.CreateProtector("Kontainr.SshCredentials");
        var dataDir = config["KONTAINR_DATA"] ?? env.ContentRootPath;
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "kontainr-settings.json");
    }

    // ── Host URL ─────────────────────────────────────────────────

    public string GetHostUrl()
    {
        lock (_lock) { return Load().HostUrl; }
    }

    public void SetHostUrl(string hostUrl)
    {
        lock (_lock)
        {
            var settings = Load();
            settings.HostUrl = hostUrl.Trim();
            Save(settings);
        }
    }

    // ── Theme ─────────────────────────────────────────────────────

    public string GetTheme()
    {
        lock (_lock) { return Load().Theme; }
    }

    public void SetTheme(string theme)
    {
        lock (_lock) { var s = Load(); s.Theme = theme; Save(s); }
    }

    // ── Export / Import ──────────────────────────────────────────

    public string ExportSettings()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(Load(), new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public void ImportSettings(string json)
    {
        lock (_lock)
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is not null)
                Save(settings);
        }
    }

    // ── Webhook ───────────────────────────────────────────────────

    public WebhookConfig GetWebhook()
    {
        lock (_lock) { return Load().Webhook; }
    }

    public void SaveWebhook(WebhookConfig config)
    {
        lock (_lock) { var s = Load(); s.Webhook = config; Save(s); }
    }

    // ── Scheduled Restarts ───────────────────────────────────────

    public List<ScheduledRestart> GetScheduledRestarts()
    {
        lock (_lock) { return Load().ScheduledRestarts.ToList(); }
    }

    public void SaveScheduledRestart(ScheduledRestart sr)
    {
        lock (_lock)
        {
            var s = Load();
            s.ScheduledRestarts.RemoveAll(x => x.Id == sr.Id);
            s.ScheduledRestarts.Add(sr);
            Save(s);
        }
    }

    public void DeleteScheduledRestart(string id)
    {
        lock (_lock)
        {
            var s = Load();
            s.ScheduledRestarts.RemoveAll(x => x.Id == id);
            Save(s);
        }
    }

    // ── Favorites ─────────────────────────────────────────────────

    public HashSet<string> GetFavorites()
    {
        lock (_lock) { return [..Load().FavoriteContainerNames]; }
    }

    public void ToggleFavorite(string containerName)
    {
        lock (_lock)
        {
            var settings = Load();
            if (!settings.FavoriteContainerNames.Remove(containerName))
                settings.FavoriteContainerNames.Add(containerName);
            Save(settings);
        }
    }

    public bool IsFavorite(string containerName)
    {
        lock (_lock) { return Load().FavoriteContainerNames.Contains(containerName); }
    }

    // ── SSH Connections ──────────────────────────────────────────

    public List<SshConnectionConfig> GetConnections()
    {
        lock (_lock) { return Load().SshConnections.ToList(); }
    }

    public SshConnectionConfig? GetConnection(string id)
    {
        lock (_lock) { return Load().SshConnections.FirstOrDefault(c => c.Id == id); }
    }

    public void SaveConnection(SshConnectionConfig config, string? plainPassword = null)
    {
        lock (_lock)
        {
            var settings = Load();
            var existing = settings.SshConnections.FirstOrDefault(c => c.Id == config.Id);

            if (plainPassword is not null)
                config.EncryptedPassword = _protector.Protect(plainPassword);
            else if (existing is not null)
                config.EncryptedPassword = existing.EncryptedPassword;

            if (existing is not null)
                settings.SshConnections.Remove(existing);

            settings.SshConnections.Add(config);
            Save(settings);
        }
    }

    public void DeleteConnection(string id)
    {
        lock (_lock)
        {
            var settings = Load();
            settings.SshConnections.RemoveAll(c => c.Id == id);
            Save(settings);
        }
    }

    public string DecryptPassword(string encrypted)
    {
        try { return _protector.Unprotect(encrypted); }
        catch { return ""; }
    }

    // ── Git Stacks ──────────────────────────────────────────────

    public List<GitStackConfig> GetGitStacks()
    {
        lock (_lock) { return Load().GitStacks.ToList(); }
    }

    public GitStackConfig? GetGitStack(string id)
    {
        lock (_lock) { return Load().GitStacks.FirstOrDefault(s => s.Id == id); }
    }

    public void SaveGitStack(GitStackConfig config, string? plainPassword = null)
    {
        lock (_lock)
        {
            var settings = Load();
            var existing = settings.GitStacks.FirstOrDefault(s => s.Id == config.Id);

            if (plainPassword is not null)
                config.EncryptedGitPassword = _protector.Protect(plainPassword);
            else if (existing is not null)
                config.EncryptedGitPassword = existing.EncryptedGitPassword;

            if (existing is not null)
                settings.GitStacks.Remove(existing);

            settings.GitStacks.Add(config);
            Save(settings);
        }
    }

    public void DeleteGitStack(string id)
    {
        lock (_lock)
        {
            var s = Load();
            s.GitStacks.RemoveAll(x => x.Id == id);
            Save(s);
        }
    }

    // ── Registries ─────────────────────────────────────────────

    public List<RegistryConfig> GetRegistries()
    {
        lock (_lock) { return Load().Registries.ToList(); }
    }

    public RegistryConfig? GetRegistry(string id)
    {
        lock (_lock) { return Load().Registries.FirstOrDefault(r => r.Id == id); }
    }

    public void SaveRegistry(RegistryConfig config, string? plainPassword = null)
    {
        lock (_lock)
        {
            var settings = Load();
            var existing = settings.Registries.FirstOrDefault(r => r.Id == config.Id);

            if (plainPassword is not null)
                config.EncryptedPassword = _protector.Protect(plainPassword);
            else if (existing is not null)
                config.EncryptedPassword = existing.EncryptedPassword;

            if (existing is not null)
                settings.Registries.Remove(existing);

            settings.Registries.Add(config);
            Save(settings);
        }
    }

    public void DeleteRegistry(string id)
    {
        lock (_lock)
        {
            var s = Load();
            s.Registries.RemoveAll(r => r.Id == id);
            Save(s);
        }
    }

    // ── Docker Hosts ──────────────────────────────────────────────

    public List<DockerHostConfig> GetDockerHosts()
    {
        lock (_lock) { return Load().DockerHosts.ToList(); }
    }

    public void SaveDockerHost(DockerHostConfig config)
    {
        lock (_lock)
        {
            var s = Load();
            var existing = s.DockerHosts.FirstOrDefault(h => h.Id == config.Id);
            if (existing is not null) s.DockerHosts.Remove(existing);
            s.DockerHosts.Add(config);
            Save(s);
        }
    }

    public void DeleteDockerHost(string id)
    {
        lock (_lock)
        {
            var s = Load();
            s.DockerHosts.RemoveAll(h => h.Id == id);
            Save(s);
        }
    }

    // ── Metrics Settings ────────────────────────────────────────

    public int GetMetricsCollectionInterval()
    {
        lock (_lock) { return Load().MetricsCollectionIntervalSeconds; }
    }

    public int GetMetricsRetentionDays()
    {
        lock (_lock) { return Load().MetricsRetentionDays; }
    }

    public void SaveMetricsSettings(int intervalSeconds, int retentionDays)
    {
        lock (_lock)
        {
            var s = Load();
            s.MetricsCollectionIntervalSeconds = intervalSeconds;
            s.MetricsRetentionDays = retentionDays;
            Save(s);
        }
    }

    // ── Log Alert Rules ──────────────────────────────────────────

    public List<LogAlertRule> GetLogAlertRules()
    {
        lock (_lock) { return Load().LogAlertRules.ToList(); }
    }

    public void SaveLogAlertRule(LogAlertRule rule)
    {
        lock (_lock)
        {
            var s = Load();
            var existing = s.LogAlertRules.FirstOrDefault(r => r.Id == rule.Id);
            if (existing is not null) s.LogAlertRules.Remove(existing);
            s.LogAlertRules.Add(rule);
            Save(s);
        }
    }

    public void DeleteLogAlertRule(string id)
    {
        lock (_lock)
        {
            var s = Load();
            s.LogAlertRules.RemoveAll(r => r.Id == id);
            Save(s);
        }
    }

    // ── Persistence ──────────────────────────────────────────────

    private AppSettings Load()
    {
        if (_cache is not null) return _cache;

        if (!File.Exists(_filePath))
        {
            _cache = new AppSettings();
            return _cache;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            _cache = new AppSettings();
        }

        return _cache;
    }

    private void Save(AppSettings settings)
    {
        _cache = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
