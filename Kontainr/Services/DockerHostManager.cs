using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Docker.DotNet;
using Docker.DotNet.X509;
using Kontainr.Models;
using Renci.SshNet;

namespace Kontainr.Services;

public class DockerHostManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ManagedDockerHost> _hosts = new();
    private readonly SshSettingsService _settings;
    private readonly ILogger<DockerHostManager> _logger;

    public DockerHostManager(SshSettingsService settings, ILogger<DockerHostManager> logger)
    {
        _settings = settings;
        _logger = logger;

        // Earlier builds wrote Docker host SSH passwords to disk in plaintext. Re-encrypt
        // anything still stored that way before we start reading credentials from it.
        var upgraded = _settings.UpgradePlaintextDockerHostPasswords();
        if (upgraded > 0)
            _logger.LogWarning(
                "Re-encrypted {Count} Docker host password(s) that an earlier version stored in plaintext. " +
                "Rotate those credentials — they were readable on disk.", upgraded);

        // Always register local host
        var localClient = OperatingSystem.IsWindows()
            ? new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient()
            : new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

        _hosts["local"] = new ManagedDockerHost
        {
            Config = new DockerHostConfig { Id = "local", Name = "Local", Type = DockerHostType.Local, IsDefault = true },
            Client = localClient
        };

        // Load saved hosts
        foreach (var hostConfig in settings.GetDockerHosts())
        {
            try
            {
                var managed = CreateManagedHost(hostConfig);
                _hosts[hostConfig.Id] = managed;
                _logger.LogInformation("Connected to Docker host: {Name} ({Id})", hostConfig.Name, hostConfig.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to Docker host: {Name}", hostConfig.Name);
            }
        }
    }

    public DockerClient GetClient(string hostId)
    {
        return _hosts.TryGetValue(hostId, out var host)
            ? host.Client
            : throw new KeyNotFoundException($"Docker host '{hostId}' not found");
    }

    public IReadOnlyList<string> GetAllHostIds() => [.. _hosts.Keys];

    public DockerHostConfig GetHostConfig(string hostId)
    {
        return _hosts.TryGetValue(hostId, out var host)
            ? host.Config
            : throw new KeyNotFoundException($"Docker host '{hostId}' not found");
    }

    public IReadOnlyList<DockerHostConfig> GetAllHostConfigs() =>
        _hosts.Values.Select(h => h.Config).ToList();

    /// <summary>
    /// Connects to a host and, once the connection is proven, persists it.
    /// </summary>
    /// <param name="plainSshPassword">
    /// The SSH password as typed, or null to reuse the one already stored for this host.
    /// It is encrypted by <see cref="SshSettingsService.SaveDockerHost"/> before it reaches disk.
    /// </param>
    public async Task AddHostAsync(DockerHostConfig config, string? plainSshPassword = null)
    {
        var managed = CreateManagedHost(config, plainSshPassword);

        // Test the connection
        await managed.Client.System.PingAsync();

        _hosts[config.Id] = managed;
        _settings.SaveDockerHost(config, plainSshPassword);
        _logger.LogInformation("Added Docker host: {Name} ({Id})", config.Name, config.Id);
    }

    public void RemoveHost(string hostId)
    {
        if (hostId == "local") throw new InvalidOperationException("Cannot remove the local Docker host");

        if (_hosts.TryRemove(hostId, out var host))
        {
            host.Dispose();
            _settings.DeleteDockerHost(hostId);
            _logger.LogInformation("Removed Docker host: {Name}", host.Config.Name);
        }
    }

    public async Task<(bool success, string message)> TestConnectionAsync(
        DockerHostConfig config, string? plainSshPassword = null)
    {
        ManagedDockerHost? managed = null;
        try
        {
            managed = CreateManagedHost(config, plainSshPassword);
            await managed.Client.System.PingAsync();
            var version = await managed.Client.System.GetVersionAsync();
            return (true, $"Connected — Docker {version.Version}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            // Only dispose if we're not keeping it
            if (managed is not null && !_hosts.ContainsKey(config.Id))
                managed.Dispose();
        }
    }

    private ManagedDockerHost CreateManagedHost(DockerHostConfig config, string? plainSshPassword = null)
    {
        return config.Type switch
        {
            DockerHostType.Tcp => CreateTcpHost(config),
            DockerHostType.SshTunnel => CreateSshTunnelHost(config, plainSshPassword),
            _ => throw new ArgumentException($"Cannot create managed host for type: {config.Type}")
        };
    }

    private ManagedDockerHost CreateTcpHost(DockerHostConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TcpEndpoint))
            throw new ArgumentException("TCP endpoint is required");

        if (!Uri.TryCreate(config.TcpEndpoint, UriKind.Absolute, out var endpoint))
            throw new ArgumentException($"'{config.TcpEndpoint}' is not a valid Docker endpoint URI");

        DockerClient client;

        if (config.TcpUseTls)
        {
            client = new DockerClientConfiguration(endpoint, CreateTlsCredentials(config)).CreateClient();
        }
        else
        {
            // A plain TCP Docker endpoint is unauthenticated and unencrypted: anyone who can
            // reach it, or sit on the path to it, owns that host. Loopback is the one case
            // where that is contained — typically a tunnel the operator set up themselves.
            if (!IsLoopback(endpoint) && !config.TcpAllowPlaintext)
                throw new InvalidOperationException(
                    $"Refusing to connect to {endpoint.Host} over plain TCP. Docker's TCP socket has no " +
                    "authentication, so this hands full control of that host to anything on the network. " +
                    "Enable TLS and point Kontainr at a certificate directory containing ca.pem, cert.pem and " +
                    "key.pem, or tick “allow plaintext” if the link is already protected some other way.");

            client = new DockerClientConfiguration(endpoint).CreateClient();
        }

        return new ManagedDockerHost { Config = config, Client = client };
    }

    /// <summary>
    /// Builds mutual-TLS credentials from a DOCKER_CERT_PATH-style directory, and pins server
    /// validation to the supplied CA rather than deferring to the machine trust store.
    /// </summary>
    private static CertificateCredentials CreateTlsCredentials(DockerHostConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TcpCertDirectory))
            throw new ArgumentException(
                "A certificate directory is required when TLS is enabled. It must contain ca.pem, cert.pem and key.pem.");

        var dir = config.TcpCertDirectory;
        var caPath = Path.Combine(dir, "ca.pem");
        var certPath = Path.Combine(dir, "cert.pem");
        var keyPath = Path.Combine(dir, "key.pem");

        foreach (var (label, path) in new[] { ("ca.pem", caPath), ("cert.pem", certPath), ("key.pem", keyPath) })
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"TLS is enabled for '{config.Name}' but {label} was not found in {dir}. " +
                    "If Kontainr runs in a container, mount the certificate directory into it.", path);
        }

        var clientCert = LoadClientCertificate(certPath, keyPath);
        var caCert = X509CertificateLoader.LoadCertificateFromFile(caPath);

        return new CertificateCredentials(clientCert)
        {
            ServerCertificateValidationCallback = (_, serverCert, _, errors) =>
                ValidateAgainstCa(serverCert, caCert, errors)
        };
    }

    private static X509Certificate2 LoadClientCertificate(string certPath, string keyPath)
    {
        var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);

        // On Windows an ephemeral key cannot be used for TLS client authentication until the
        // certificate has made a round trip through PKCS#12.
        if (OperatingSystem.IsWindows())
        {
            using (cert)
                return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12), null);
        }

        return cert;
    }

    private static bool ValidateAgainstCa(
        X509Certificate? serverCertificate, X509Certificate2 caCertificate, SslPolicyErrors errors)
    {
        if (serverCertificate is null) return false;

        // Only chain trust is delegated to the pinned CA — a name mismatch is still a mismatch.
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)) return false;
        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable)) return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(caCertificate);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        using var server = X509CertificateLoader.LoadCertificate(serverCertificate.GetRawCertData());
        return chain.Build(server);
    }

    private static bool IsLoopback(Uri endpoint)
    {
        if (endpoint.IsLoopback) return true;
        return IPAddress.TryParse(endpoint.Host, out var ip) && IPAddress.IsLoopback(ip);
    }

    private ManagedDockerHost CreateSshTunnelHost(DockerHostConfig config, string? plainSshPassword = null)
    {
        if (string.IsNullOrWhiteSpace(config.SshHost) || string.IsNullOrWhiteSpace(config.SshUsername))
            throw new ArgumentException("SSH host and username are required");

        string password;
        if (plainSshPassword is not null)
        {
            password = plainSshPassword;
        }
        else if (!string.IsNullOrEmpty(config.EncryptedSshPassword))
        {
            // Fail loudly rather than falling back to an empty password, which surfaces as a
            // confusing authentication failure against the remote host.
            password = _settings.TryDecryptPassword(config.EncryptedSshPassword)
                ?? throw new InvalidOperationException(
                    $"The stored password for '{config.Name}' could not be decrypted. That happens when the Data " +
                    "Protection key ring is missing from the data volume, or was created under a different " +
                    "KONTAINR_KEY_PASSPHRASE. Re-enter the password for this host.");
        }
        else
        {
            // No password stored — key-based authentication is expected.
            password = "";
        }

        var sshClient = new SshClient(config.SshHost, config.SshPort, config.SshUsername, password);
        sshClient.Connect();

        // Forward a local TCP port to the remote Docker socket
        var forwardedPort = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", 2375);
        sshClient.AddForwardedPort(forwardedPort);
        forwardedPort.Start();

        var dockerEndpoint = $"tcp://127.0.0.1:{forwardedPort.BoundPort}";
        var dockerClient = new DockerClientConfiguration(new Uri(dockerEndpoint)).CreateClient();

        return new ManagedDockerHost
        {
            Config = config,
            Client = dockerClient,
            SshClient = sshClient,
            ForwardedPort = forwardedPort
        };
    }

    public void Dispose()
    {
        foreach (var host in _hosts.Values)
            host.Dispose();
        _hosts.Clear();
    }

    private class ManagedDockerHost : IDisposable
    {
        public required DockerHostConfig Config { get; init; }
        public required DockerClient Client { get; init; }
        public SshClient? SshClient { get; init; }
        public ForwardedPortLocal? ForwardedPort { get; init; }

        public void Dispose()
        {
            Client.Dispose();
            if (ForwardedPort is { IsStarted: true }) ForwardedPort.Stop();
            if (SshClient is { IsConnected: true }) SshClient.Disconnect();
            SshClient?.Dispose();
        }
    }
}
