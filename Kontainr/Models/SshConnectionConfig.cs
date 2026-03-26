namespace Kontainr.Models;

public class SshConnectionConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public List<string> InitCommands { get; set; } = [];
}

public class AppSettings
{
    public string HostUrl { get; set; } = "localhost";
    public string Theme { get; set; } = "dark";
    public List<SshConnectionConfig> SshConnections { get; set; } = [];
    public HashSet<string> FavoriteContainerNames { get; set; } = [];
    public WebhookConfig Webhook { get; set; } = new();
    public List<ScheduledRestart> ScheduledRestarts { get; set; } = [];
    public List<GitStackConfig> GitStacks { get; set; } = [];
    public List<RegistryConfig> Registries { get; set; } = [];
    public List<LogAlertRule> LogAlertRules { get; set; } = [];
    public List<DockerHostConfig> DockerHosts { get; set; } = [];
    public int MetricsCollectionIntervalSeconds { get; set; } = 15;
    public int MetricsRetentionDays { get; set; } = 7;
}

public class WebhookConfig
{
    public string Url { get; set; } = "";
    public bool Enabled { get; set; }
    public bool OnCrash { get; set; } = true;
    public bool OnRestartLoop { get; set; } = true;
    public bool OnLogPattern { get; set; } = true;
}

public class LogAlertRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string ContainerName { get; set; } = "";
    public string Pattern { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class ScheduledRestart
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string ContainerName { get; set; } = "";
    public string CronExpression { get; set; } = "";
    public string DisplaySchedule { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
