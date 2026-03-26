using System.Collections.Concurrent;
using Docker.DotNet;
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

    public async Task AddHostAsync(DockerHostConfig config)
    {
        var managed = CreateManagedHost(config);

        // Test the connection
        await managed.Client.System.PingAsync();

        _hosts[config.Id] = managed;
        _settings.SaveDockerHost(config);
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

    public async Task<(bool success, string message)> TestConnectionAsync(DockerHostConfig config)
    {
        ManagedDockerHost? managed = null;
        try
        {
            managed = CreateManagedHost(config);
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

    private ManagedDockerHost CreateManagedHost(DockerHostConfig config)
    {
        return config.Type switch
        {
            DockerHostType.Tcp => CreateTcpHost(config),
            DockerHostType.SshTunnel => CreateSshTunnelHost(config),
            _ => throw new ArgumentException($"Cannot create managed host for type: {config.Type}")
        };
    }

    private ManagedDockerHost CreateTcpHost(DockerHostConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.TcpEndpoint))
            throw new ArgumentException("TCP endpoint is required");

        var client = new DockerClientConfiguration(new Uri(config.TcpEndpoint)).CreateClient();
        return new ManagedDockerHost { Config = config, Client = client };
    }

    private ManagedDockerHost CreateSshTunnelHost(DockerHostConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SshHost) || string.IsNullOrWhiteSpace(config.SshUsername))
            throw new ArgumentException("SSH host and username are required");

        var password = !string.IsNullOrEmpty(config.EncryptedSshPassword)
            ? _settings.DecryptPassword(config.EncryptedSshPassword)
            : "";

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
