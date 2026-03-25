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
