namespace Kontainr.Services;

/// <summary>
/// Directory creation for locations holding secrets (the settings file, the Data
/// Protection key ring). Everything Kontainr persists is sensitive enough that it
/// should not be group- or world-readable.
/// </summary>
public static class SecureDirectory
{
    /// <summary>
    /// Creates the directory if needed and, on Unix, restricts it to the owner (0700).
    /// Permission changes are best-effort: a bind-mounted host directory may be owned by
    /// another user, and failing to tighten it is not a reason to refuse to start.
    /// </summary>
    public static DirectoryInfo Create(string path, ILogger? logger = null)
    {
        var info = Directory.CreateDirectory(path);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "Could not restrict permissions on {Path}. Ensure it is not readable by other users — " +
                    "it holds credentials and encryption keys.", path);
            }
        }

        return info;
    }
}
