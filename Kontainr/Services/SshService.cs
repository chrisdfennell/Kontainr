using Renci.SshNet;
using Kontainr.Models;

namespace Kontainr.Services;

public class SshSessionManager : IDisposable
{
    private readonly Dictionary<string, SshSession> _sessions = [];
    private readonly SshSettingsService _settings;

    public SshSessionManager(SshSettingsService settings)
    {
        _settings = settings;
    }

    public SshSession GetOrCreateSession(string connectionId)
    {
        if (_sessions.TryGetValue(connectionId, out var existing) && existing.IsConnected)
            return existing;

        // Clean up stale session
        if (existing is not null)
        {
            existing.Dispose();
            _sessions.Remove(connectionId);
        }

        var config = _settings.GetConnection(connectionId)
            ?? throw new InvalidOperationException($"Connection '{connectionId}' not found");

        var password = _settings.DecryptPassword(config.EncryptedPassword);
        var session = new SshSession(config.Host, config.Port, config.Username, password);
        session.Connect();
        _sessions[connectionId] = session;
        return session;
    }

    public void DisconnectSession(string connectionId)
    {
        if (_sessions.Remove(connectionId, out var session))
            session.Dispose();
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
            session.Dispose();
        _sessions.Clear();
    }
}

public class SshSession : IDisposable
{
    private readonly SshClient _client;
    private ShellStream? _shell;

    public bool IsConnected => _client.IsConnected;
    public string Host { get; }

    public SshSession(string host, int port, string username, string password)
    {
        Host = host;
        _client = new SshClient(host, port, username, password);
        _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
    }

    public void Connect()
    {
        _client.Connect();
        _shell = _client.CreateShellStream("kontainr", 120, 40, 120, 40, 4096);
        // Wait for initial prompt
        Thread.Sleep(500);
        // Drain initial output
        if (_shell.DataAvailable)
            _shell.Read();
    }

    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (_shell is null || !_client.IsConnected)
            throw new InvalidOperationException("Not connected");

        // Clear any pending data
        if (_shell.DataAvailable)
            _shell.Read();

        _shell.WriteLine(command);

        // Wait for output
        var output = new System.Text.StringBuilder();
        var timeout = DateTime.UtcNow.AddSeconds(10);
        await Task.Delay(200);

        while (DateTime.UtcNow < timeout)
        {
            if (_shell.DataAvailable)
            {
                var data = _shell.Read();
                output.Append(data);
                // Wait a bit more for additional output
                await Task.Delay(100);
                if (!_shell.DataAvailable)
                    break;
            }
            else
            {
                await Task.Delay(50);
            }
        }

        // Clean up the output: remove the command echo and prompt
        var result = output.ToString();
        var lines = result.Split('\n');
        if (lines.Length > 1)
        {
            // First line is usually the echoed command, last line is the prompt
            var cleaned = lines.Skip(1).SkipLast(1);
            return string.Join('\n', cleaned).Trim();
        }

        return result.Trim();
    }

    public void Dispose()
    {
        _shell?.Dispose();
        if (_client.IsConnected)
            _client.Disconnect();
        _client.Dispose();
    }
}
