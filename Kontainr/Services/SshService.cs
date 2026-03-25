using System.Text.RegularExpressions;
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
        _shell = _client.CreateShellStream("xterm", 200, 50, 200, 50, 16384);

        // Wait for initial output to settle (login banner, MOTD, menus)
        WaitForOutput(2000);
        DrainAll();

        // Execute user-configured init commands (e.g. "Q", "Y" to escape QNAP menu)
        if (initCommands is { Count: > 0 })
        {
            foreach (var cmd in initCommands)
            {
                _shell.WriteLine(cmd);
                WaitForOutput(1500);
                DrainAll();
            }
            // Extra wait after all init commands for the shell to fully settle
            Thread.Sleep(1000);
            DrainAll();
        }

        // Confirm shell is ready by sending a known command
        _shell.WriteLine("echo __KONTAINR_READY__");
        WaitForOutput(1000);
        var readyOutput = DrainAll();

        // Extract the prompt pattern
        var readyLines = readyOutput.Split('\n');
        foreach (var line in readyLines)
        {
            var clean = StripAnsiAndTui(line).Trim();
            if (clean.Length > 0 && clean.Length < 80
                && !clean.Contains("__KONTAINR_READY__")
                && !clean.Contains("echo ")
                && (clean.EndsWith('$') || clean.EndsWith('#') || clean.EndsWith('>')))
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
        DrainAll();

        // Use a unique marker to delimit output
        var marker = $"__KTR_{Guid.NewGuid():N}__";
        _shell.WriteLine($"{command}; echo {marker}");

        var output = new System.Text.StringBuilder();
        var timeout = DateTime.UtcNow.AddSeconds(30);
        await Task.Delay(200);

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
                await Task.Delay(80);
                // Check one more time after a longer pause
                if (!_shell.DataAvailable && output.ToString().Contains(marker))
                    break;
            }
        }

        return CleanOutput(output.ToString(), command, marker);
    }

    private string CleanOutput(string raw, string command, string marker)
    {
        var cleaned = StripAnsiAndTui(raw);
        var lines = cleaned.Split('\n');
        var result = new List<string>();
        var foundCommand = false;
        var cmdWithMarker = $"{command}; echo {marker}";

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            // Skip the echoed command line (may include the prompt prefix)
            if (!foundCommand && (trimmed.Contains(cmdWithMarker) || trimmed.EndsWith(command)))
            {
                foundCommand = true;
                continue;
            }

            // Skip marker lines
            if (trimmed.Contains(marker) || trimmed.Contains($"echo {marker}"))
                continue;

            // Skip empty prompt-only lines
            if (trimmed == _lastPrompt.Trim())
                continue;

            if (foundCommand)
                result.Add(line);
        }

        // Remove trailing empty/prompt lines
        while (result.Count > 0)
        {
            var last = result[^1].Trim();
            if (last == "" || last == _lastPrompt.Trim()
                || last.EndsWith("$ ") || last.EndsWith("# ") || last.EndsWith("> "))
                result.RemoveAt(result.Count - 1);
            else
                break;
        }

        return string.Join('\n', result).Trim();
    }

    /// <summary>
    /// Wait for data to arrive, with a max wait time.
    /// </summary>
    private void WaitForOutput(int maxMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_shell?.DataAvailable == true)
            {
                // Data is coming — wait a bit more for it to finish
                Thread.Sleep(200);
                return;
            }
            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Drain all available output from the shell, waiting for silence.
    /// </summary>
    private string DrainAll()
    {
        if (_shell is null) return "";
        var sb = new System.Text.StringBuilder();

        // Read in a loop until no more data arrives
        for (int i = 0; i < 20; i++)
        {
            if (_shell.DataAvailable)
            {
                sb.Append(_shell.Read());
                Thread.Sleep(100);
            }
            else
            {
                // Wait a bit to see if more data comes
                Thread.Sleep(150);
                if (_shell.DataAvailable)
                {
                    sb.Append(_shell.Read());
                    Thread.Sleep(100);
                }
                else
                {
                    break;
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Strip ANSI escape sequences, TUI box-drawing characters, and control chars.
    /// </summary>
    private static string StripAnsiAndTui(string input)
    {
        // Remove all ANSI escape sequences
        var result = AnsiEscapeRegex().Replace(input, "");
        // Remove OSC sequences (title setting etc)
        result = OscRegex().Replace(result, "");
        // Remove remaining CSI sequences
        result = CsiRegex().Replace(result, "");
        // Remove box-drawing border lines (lines that are mostly +, -, |, spaces)
        var lines = result.Split('\n');
        var cleaned = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip lines that are box borders
            if (IsBoxLine(trimmed))
                continue;
            // Remove leading/trailing pipe characters from TUI
            var stripped = trimmed;
            if (stripped.StartsWith('|'))
                stripped = stripped[1..];
            if (stripped.EndsWith('|'))
                stripped = stripped[..^1];
            // If after stripping pipes the line is just whitespace/borders, skip it
            if (IsBoxLine(stripped.Trim()))
                continue;
            // Remove control characters except newline
            stripped = ControlCharRegex().Replace(stripped, "");
            cleaned.Add(stripped);
        }
        return string.Join('\n', cleaned);
    }

    private static bool IsBoxLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        // A box line is made up of +, -, |, spaces, and maybe >>
        var boxChars = 0;
        foreach (var c in line)
        {
            if (c is '+' or '-' or '|' or ' ' or '>')
                boxChars++;
        }
        return boxChars > line.Length * 0.85;
    }

    [GeneratedRegex(@"\x1B\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiEscapeRegex();

    [GeneratedRegex(@"\x1B\][^\x07]*(\x07|\x1B\\)")]
    private static partial Regex OscRegex();

    [GeneratedRegex(@"\x1B[\[\(][0-9;?]*[A-Za-z]")]
    private static partial Regex CsiRegex();

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
