namespace Kontainr.Models;

public class DockerHostConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public DockerHostType Type { get; set; } = DockerHostType.Local;
    public string? TcpEndpoint { get; set; }
    public bool TcpUseTls { get; set; }
    public string? SshHost { get; set; }
    public int SshPort { get; set; } = 22;
    public string? SshUsername { get; set; }
    public string? EncryptedSshPassword { get; set; }
    public string? HostUrl { get; set; }
    public bool IsDefault { get; set; }
}

public enum DockerHostType
{
    Local,
    Tcp,
    SshTunnel
}
