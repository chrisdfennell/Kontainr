namespace Kontainr.Models;

public class GitStackConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string GitUrl { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string ComposePath { get; set; } = "docker-compose.yml";
    public string ProjectName { get; set; } = "";
    public Dictionary<string, string> EnvVars { get; set; } = [];
    public string GitUsername { get; set; } = "";
    public string EncryptedGitPassword { get; set; } = "";
    public DateTime? LastDeployed { get; set; }
}
