namespace Kontainr.Models;

public enum RegistryType
{
    DockerHub,
    GitLab,
    Acr,
    Gcr,
    Custom
}

public class RegistryConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public RegistryType Type { get; set; } = RegistryType.Custom;
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public string Namespace { get; set; } = "";
}

public class RegistryRepository
{
    public string Name { get; set; } = "";
    public List<string> Tags { get; set; } = [];
}

public class RegistryTagDetail
{
    public string Tag { get; set; } = "";
    public string Digest { get; set; } = "";
    public long Size { get; set; }
    public string Architecture { get; set; } = "";
    public string Os { get; set; } = "";
    public DateTime? Created { get; set; }
}
