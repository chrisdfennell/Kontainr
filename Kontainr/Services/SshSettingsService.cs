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
        // Sanitised on read as well as write, so a settings file poisoned by an earlier
        // build cannot inject into the inline theme script in MainLayout.
        lock (_lock)
        {
            var theme = Load().Theme;
            return theme is "dark" or "light" ? theme : "dark";
        }
    }

    /// <summary>
    /// Stores the UI theme. The value is reached through a [JSInvokable] callback, so the
    /// browser controls it, and MainLayout interpolates it into an inline script — anything
    /// other than a known theme name is rejected rather than persisted.
    /// </summary>
    public void SetTheme(string theme)
    {
        if (theme is not ("dark" or "light"))
            throw new ArgumentException($"Unknown theme '{theme}'.", nameof(theme));

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

    /// <summary>
    /// Decrypts a stored secret, returning null when the value cannot be decrypted rather
    /// than silently degrading to an empty string. Callers that use the result to
    /// authenticate should surface the failure instead of attempting a blank credential.
    /// </summary>
    public string? TryDecryptPassword(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try { return _protector.Unprotect(encrypted); }
        catch { return null; }
    }

    /// <summary>
    /// Re-encrypts any Docker host password that was persisted in plaintext by an earlier
    /// build (which assigned the raw password straight to EncryptedSshPassword). Returns
    /// the number of entries upgraded.
    /// </summary>
    public int UpgradePlaintextDockerHostPasswords()
    {
        lock (_lock)
        {
            var s = Load();
            var upgraded = 0;

            foreach (var host in s.DockerHosts)
            {
                if (string.IsNullOrEmpty(host.EncryptedSshPassword)) continue;

                try { _protector.Unprotect(host.EncryptedSshPassword); }
                catch
                {
                    // Not decryptable: either legacy plaintext or written under a key ring
                    // we no longer have. Re-protecting is correct in the first case and
                    // harmless in the second, where the value was already unusable.
                    host.EncryptedSshPassword = _protector.Protect(host.EncryptedSshPassword);
                    upgraded++;
                }
            }

            if (upgraded > 0) Save(s);
            return upgraded;
        }
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

    /// <summary>
    /// Persists a Docker host. When <paramref name="plainSshPassword"/> is supplied it is
    /// encrypted here; when it is null the previously stored ciphertext is carried over so
    /// an edit that leaves the password field blank does not wipe it.
    /// </summary>
    public void SaveDockerHost(DockerHostConfig config, string? plainSshPassword = null)
    {
        lock (_lock)
        {
            var s = Load();
            var existing = s.DockerHosts.FirstOrDefault(h => h.Id == config.Id);

            if (plainSshPassword is not null)
                config.EncryptedSshPassword = _protector.Protect(plainSshPassword);
            else if (existing is not null)
                config.EncryptedSshPassword = existing.EncryptedSshPassword;

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

    // ── Time Zone ──────────────────────────────────────────────

    public string GetTimeZoneId()
    {
        lock (_lock) { return Load().TimeZoneId; }
    }

    public void SetTimeZoneId(string timeZoneId)
    {
        lock (_lock) { var s = Load(); s.TimeZoneId = timeZoneId; Save(s); }
    }

    public TimeZoneInfo GetTimeZone()
    {
        var id = GetTimeZoneId();
        if (string.IsNullOrEmpty(id)) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
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
