using System.Diagnostics;
using Kontainr.Services;

namespace Kontainr.Tests;

public class ShQuoteTests
{
    [Theory]
    [InlineData("main", "'main'")]
    [InlineData("release/2.0", "'release/2.0'")]
    [InlineData("", "''")]
    [InlineData("with space", "'with space'")]
    [InlineData("$HOME", "'$HOME'")]
    [InlineData("`id`", "'`id`'")]
    [InlineData("a;rm -rf /", "'a;rm -rf /'")]
    public void Wraps_values_in_single_quotes(string input, string expected)
    {
        Assert.Equal(expected, Sh.Quote(input));
    }

    [Fact]
    public void Escapes_embedded_single_quotes()
    {
        // The classic break-out: a value that closes the quote and appends a command.
        // The trailing quote becomes '\'' and the value stays wrapped, so sh sees one argument.
        Assert.Equal(@"'; touch /tmp/pwned; '\'''", Sh.Quote("; touch /tmp/pwned; '"));
    }

    [Fact]
    public void Null_becomes_an_empty_argument()
    {
        Assert.Equal("''", Sh.Quote(null));
    }

    [Fact]
    public void QuoteAll_joins_quoted_values()
    {
        Assert.Equal("'-v' 'a b' 'c'", Sh.QuoteAll(["-v", "a b", "c"]));
    }

    /// <summary>
    /// Round-trips a hostile value through a real shell to prove it arrives as one literal
    /// argument. Skipped on Windows, where there is no sh to run it against.
    /// </summary>
    [Theory]
    [InlineData("'; id #")]
    [InlineData("$(id)")]
    [InlineData("plain")]
    [InlineData("two words")]
    [InlineData(@"back\slash")]
    public void Quoted_values_survive_a_real_shell_intact(string value)
    {
        // No POSIX shell to test against on Windows; CI runs this on Linux.
        if (OperatingSystem.IsWindows()) return;

        var psi = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add($"printf '%s' {Sh.Quote(value)}");

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(value, stdout);
    }
}
