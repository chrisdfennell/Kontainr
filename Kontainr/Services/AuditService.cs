using System.Text.Json;

namespace Kontainr.Services;

public class AuditService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private List<AuditEntry>? _cache;
    private const int MaxEntries = 500;

    public AuditService(IConfiguration config, IWebHostEnvironment env)
    {
        var dataDir = config["KONTAINR_DATA"] ?? Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "audit-log.json");
    }

    public void Log(string action, string target, string? detail = null)
    {
        lock (_lock)
        {
            var entries = Load();
            entries.Insert(0, new AuditEntry
            {
                Timestamp = DateTime.UtcNow,
                Action = action,
                Target = target,
                Detail = detail
            });

            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            Save(entries);
        }
    }

    public List<AuditEntry> GetEntries(int count = 100)
    {
        lock (_lock)
        {
            return Load().Take(count).ToList();
        }
    }

    private List<AuditEntry> Load()
    {
        if (_cache is not null) return _cache;
        if (!File.Exists(_filePath)) { _cache = []; return _cache; }
        try
        {
            var json = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? [];
        }
        catch { _cache = []; }
        return _cache;
    }

    private void Save(List<AuditEntry> entries)
    {
        _cache = entries;
        File.WriteAllText(_filePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Detail { get; set; }
}
