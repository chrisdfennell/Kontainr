using System.Formats.Tar;
using Kontainr.Models;

namespace Kontainr.Services;

public class ContainerFileService(DockerService docker)
{
    public async Task<List<FileEntry>> ListDirectoryAsync(string containerId, string path)
    {
        var output = await docker.ExecContainerAsync(containerId,
            $"ls -la --time-style=long-iso {EscapePath(path)} 2>/dev/null || ls -la {EscapePath(path)}");

        var entries = new List<FileEntry>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("total ") || line.StartsWith("ls:")) continue;
            var entry = ParseLsLine(line);
            if (entry is not null && entry.Name != "." && entry.Name != "..")
                entries.Add(entry);
        }
        return entries;
    }

    public async Task<(Stream stream, string fileName)> DownloadFileAsync(string containerId, string path)
    {
        var response = await docker.GetArchiveAsync(containerId, path);
        return (response.Stream, Path.GetFileName(path));
    }

    public async Task UploadFileAsync(string containerId, string destPath, string fileName, Stream content)
    {
        var memStream = new MemoryStream();

        await using (var tarWriter = new TarWriter(memStream, leaveOpen: true))
        {
            var contentBytes = new MemoryStream();
            await content.CopyToAsync(contentBytes);
            contentBytes.Position = 0;

            var entry = new PaxTarEntry(TarEntryType.RegularFile, fileName)
            {
                DataStream = contentBytes
            };
            await tarWriter.WriteEntryAsync(entry);
        }

        memStream.Position = 0;
        await docker.ExtractArchiveAsync(containerId, destPath, memStream);
    }

    public async Task DeleteAsync(string containerId, string path)
    {
        await docker.ExecContainerAsync(containerId, $"rm -rf {EscapePath(path)}");
    }

    public async Task CreateDirectoryAsync(string containerId, string path)
    {
        await docker.ExecContainerAsync(containerId, $"mkdir -p {EscapePath(path)}");
    }

    private static string EscapePath(string path)
    {
        // Shell-escape single quotes in path
        return "'" + path.Replace("'", "'\\''") + "'";
    }

    private static FileEntry? ParseLsLine(string line)
    {
        // Format: drwxr-xr-x 2 root root 4096 2024-01-15 10:30 dirname
        // Or:     lrwxrwxrwx 1 root root   11 2024-01-15 10:30 link -> target
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8) return null;

        var perms = parts[0];
        var isDir = perms.StartsWith('d');
        var isSymlink = perms.StartsWith('l');

        // Find the name - it's after the date/time fields
        // Owner is parts[2], group is parts[3], size is parts[4]
        // Date could be parts[5] and time parts[6], then name starts at parts[7]
        // Or in non-long-iso format: month day time/year then name

        if (!long.TryParse(parts[4], out var size))
            size = 0;

        // Try to find the filename - look for the date pattern
        var nameStartIndex = -1;
        for (int i = 5; i < parts.Length - 1; i++)
        {
            // long-iso format: 2024-01-15 10:30
            if (parts[i].Length == 10 && parts[i][4] == '-' && i + 1 < parts.Length)
            {
                nameStartIndex = i + 2;
                break;
            }
            // standard format: Jan 15 10:30 or Jan 15 2024
            if (parts[i].Length == 3 && char.IsLetter(parts[i][0]) && i + 2 < parts.Length)
            {
                nameStartIndex = i + 3;
                break;
            }
        }

        if (nameStartIndex < 0 || nameStartIndex >= parts.Length)
        {
            // Fallback: name is last element
            nameStartIndex = parts.Length - 1;
        }

        var namePart = string.Join(' ', parts[nameStartIndex..]);
        var name = namePart;
        var symlinkTarget = "";

        if (isSymlink && namePart.Contains(" -> "))
        {
            var arrowIdx = namePart.IndexOf(" -> ", StringComparison.Ordinal);
            name = namePart[..arrowIdx];
            symlinkTarget = namePart[(arrowIdx + 4)..];
        }

        var modified = "";
        if (nameStartIndex >= 2)
        {
            // Try to extract date from parts before name
            for (int i = 5; i < nameStartIndex; i++)
                modified += (modified.Length > 0 ? " " : "") + parts[i];
        }

        return new FileEntry
        {
            Name = name,
            IsDirectory = isDir,
            IsSymlink = isSymlink,
            SymlinkTarget = symlinkTarget,
            Size = size,
            Permissions = perms,
            Owner = parts.Length > 2 ? parts[2] : "",
            Group = parts.Length > 3 ? parts[3] : "",
            Modified = modified
        };
    }
}
