namespace Kontainr.Models;

public class FileEntry
{
    public string Name { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public string Permissions { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Group { get; set; } = "";
    public string Modified { get; set; } = "";
    public bool IsSymlink { get; set; }
    public string SymlinkTarget { get; set; } = "";
}
