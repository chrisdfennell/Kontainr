namespace Kontainr.Models;

public class DockerHostConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public DockerHostType Type { get; set; } = DockerHostType.Local;
    public string? TcpEndpoint { get; set; }

    /// <summary>
    /// Verify and authenticate the TCP connection with mutual TLS, using the ca.pem,
    /// cert.pem and key.pem found in <see cref="TcpCertDirectory"/>.
    /// </summary>
    public bool TcpUseTls { get; set; }

    /// <summary>
    /// Directory holding ca.pem, cert.pem and key.pem — the same layout Docker's own
    /// DOCKER_CERT_PATH uses. Must be readable by Kontainr, so mount it into the container.
    /// </summary>
    public string? TcpCertDirectory { get; set; }

    /// <summary>
    /// Opt in to an unencrypted, unauthenticated TCP connection to a non-loopback host.
    /// An open Docker TCP port is full control of that host and the traffic is readable
    /// and modifiable by anything on the path, so this is refused unless set deliberately.
    /// </summary>
    public bool TcpAllowPlaintext { get; set; }

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
