using Kontainr.Services;

namespace Kontainr.Tests;

public class LogAlertMatchingTests
{
    private const string SampleLogs =
        "2026-06-27 INFO  starting up\n" +
        "2026-06-27 WARN  disk almost full\n" +
        "2026-06-27 ERROR connection refused\n";

    [Fact]
    public void Returns_first_line_matching_a_regex()
    {
        var line = LogAlertService.FindMatchingLine(SampleLogs, "ERROR");
        Assert.NotNull(line);
        Assert.Contains("connection refused", line);
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var line = LogAlertService.FindMatchingLine(SampleLogs, "error");
        Assert.NotNull(line);
    }

    [Fact]
    public void Returns_null_when_no_line_matches()
    {
        Assert.Null(LogAlertService.FindMatchingLine(SampleLogs, "fatal"));
    }

    [Fact]
    public void Supports_regex_alternation()
    {
        var line = LogAlertService.FindMatchingLine(SampleLogs, "WARN|ERROR");
        Assert.NotNull(line);
        Assert.Contains("disk almost full", line); // first matching line wins
    }

    [Fact]
    public void Invalid_regex_falls_back_to_substring_match()
    {
        // "[" is invalid regex; should fall back to literal substring search and not throw
        var logs = "line with a literal [ bracket\n";
        var line = LogAlertService.FindMatchingLine(logs, "[");
        Assert.NotNull(line);
    }

    [Theory]
    [InlineData("", "ERROR")]
    [InlineData("some logs", "")]
    public void Empty_input_returns_null(string logs, string pattern)
    {
        Assert.Null(LogAlertService.FindMatchingLine(logs, pattern));
    }
}
