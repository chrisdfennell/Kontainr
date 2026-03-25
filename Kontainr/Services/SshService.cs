using System.Text.RegularExpressions;
using Renci.SshNet;
using Kontainr.Models;

namespace Kontainr.Services;

public partial class SshSessionManager : IDisposable
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

        if (existing is not null)
        {
            existing.Dispose();
            _sessions.Remove(connectionId);
        }

        var config = _settings.GetConnection(connectionId)
            ?? throw new InvalidOperationException($"Connection '{connectionId}' not found");

        var password = _settings.DecryptPassword(config.EncryptedPassword);
        var session = new SshSession(config.Host, config.Port, config.Username, password);
        session.Connect(config.InitCommands);
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

public partial class SshSession : IDisposable
{
    private readonly SshClient _client;
    private ShellStream? _shell;
    private string _lastPrompt = "$ ";

    public bool IsConnected => _client.IsConnected;
    public string Host { get; }

    public SshSession(string host, int port, string username, string password)
    {
        Host = host;
        _client = new SshClient(host, port, username, password);
        _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
    }

    public void Connect(List<string>? initCommands = null)
    {
        _client.Connect();
        _shell = _client.CreateShellStream("xterm", 200, 50, 200, 50, 8192);

        // Wait for initial output (login banner, MOTD, menus)
        Thread.Sleep(1500);
        DrainOutput();

        // Execute user-configured init commands (e.g. "Q", "Y" to escape QNAP menu)
        if (initCommands is { Count: > 0 })
        {
            foreach (var cmd in initCommands)
            {
                _shell.WriteLine(cmd);
                Thread.Sleep(800);
                DrainOutput();
            }
        }

        // Send a marker command to find the prompt and confirm shell is ready
        _shell.WriteLine("echo __KONTAINR_READY__");
        Thread.Sleep(500);
        var readyOutput = DrainOutput();

        // Extract the prompt pattern from the output
        var readyLines = readyOutput.Split('\n');
        foreach (var line in readyLines)
        {
            var clean = StripAnsi(line).Trim();
            if (clean.Length > 0 && !clean.Contains("__KONTAINR_READY__") && !clean.Contains("echo "))
            {
                _lastPrompt = clean;
                break;
            }
        }
    }

    public async Task<string> ExecuteCommandAsync(string command)
    {
        if (_shell is null || !_client.IsConnected)
            throw new InvalidOperationException("Not connected");

        // Drain any leftover data
        DrainOutput();

        // Use a unique marker to delimit output
        var marker = $"__KONTAINR_{Guid.NewGuid():N}__";
        _shell.WriteLine($"{command}; echo {marker}");

        var output = new System.Text.StringBuilder();
        var timeout = DateTime.UtcNow.AddSeconds(30);
        await Task.Delay(150);

        while (DateTime.UtcNow < timeout)
        {
            if (_shell.DataAvailable)
            {
                var data = _shell.Read();
                output.Append(data);

                if (output.ToString().Contains(marker))
                    break;

                await Task.Delay(50);
            }
            else
            {
                await Task.Delay(50);
            }
        }

        return CleanOutput(output.ToString(), command, marker);
    }

    private string CleanOutput(string raw, string command, string marker)
    {
        // Strip ANSI escape codes
        var cleaned = StripAnsi(raw);

        var lines = cleaned.Split('\n').ToList();
        var result = new List<string>();
        var foundCommand = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Skip the echoed command line
            if (!foundCommand && (line.Contains(command) || line.Contains($"{command}; echo {marker}")))
            {
                foundCommand = true;
                continue;
            }

            // Skip marker lines
            if (line.Contains(marker))
                continue;

            // Skip prompt-only lines at the end
            if (line.Trim() == _lastPrompt.Trim())
                continue;

            // Skip lines that are just the prompt with the marker
            if (line.Contains("echo " + marker))
                continue;

            if (foundCommand)
                result.Add(line);
        }

        // Remove trailing empty lines and prompt lines
        while (result.Count > 0)
        {
            var last = result[^1].Trim();
            if (last == "" || last == _lastPrompt.Trim() || last.EndsWith("$ ") || last.EndsWith("# "))
                result.RemoveAt(result.Count - 1);
            else
                break;
        }

        return string.Join('\n', result).Trim();
    }

    private string DrainOutput()
    {
        if (_shell is null) return "";
        var sb = new System.Text.StringBuilder();
        while (_shell.DataAvailable)
        {
            sb.Append(_shell.Read());
            Thread.Sleep(50);
        }
        return sb.ToString();
    }

    private static string StripAnsi(string input)
    {
        // Remove ANSI escape sequences, box drawing artifacts, and control chars
        var result = AnsiRegex().Replace(input, "");
        // Remove remaining control characters except newline/tab
        result = ControlCharRegex().Replace(result, "");
        return result;
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]|\x1B\].*?(\x07|\x1B\\)|\x1B[()][0-9A-B]|\x1B\[[\?]?[0-9;]*[hlm]")]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"[\x00-\x08\x0E-\x1F]")]
    private static partial Regex ControlCharRegex();

    public void Dispose()
    {
        _shell?.Dispose();
        if (_client.IsConnected)
            _client.Disconnect();
        _client.Dispose();
    }
}
