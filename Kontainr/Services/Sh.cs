namespace Kontainr.Services;

/// <summary>
/// Helpers for building POSIX shell command strings.
///
/// Several Kontainr operations work by handing an <c>sh -c</c> script to a throwaway
/// container that has the Docker socket bound, so an unquoted value inside one of those
/// scripts is a direct path to command execution on the Docker host. Prefer passing
/// values in as environment variables and referencing them as "$VAR" in the script;
/// use <see cref="Quote"/> when a value genuinely has to be interpolated.
/// </summary>
public static class Sh
{
    /// <summary>
    /// Wraps a value in single quotes so the shell treats it as a single literal argument.
    /// Embedded single quotes are closed, escaped and reopened (<c>'\''</c>), which is the
    /// only escape sequence sh honours inside single quotes.
    /// </summary>
    public static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "''";
        return "'" + value.Replace("'", @"'\''") + "'";
    }

    /// <summary>
    /// Quotes each value and joins them with spaces.
    /// </summary>
    public static string QuoteAll(IEnumerable<string> values) =>
        string.Join(' ', values.Select(Quote));
}
