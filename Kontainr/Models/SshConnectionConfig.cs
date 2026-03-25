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
    public List<SshConnectionConfig> SshConnections { get; set; } = [];
}
