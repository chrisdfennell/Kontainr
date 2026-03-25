using Docker.DotNet.Models;

namespace Kontainr.Models;

public class ContainerViewModel
{
    public string Id { get; set; } = "";
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
    public string Name { get; set; } = "";
    public string Image { get; set; } = "";
    public string State { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ComposeProject { get; set; }
    public string? ComposeService { get; set; }
    public DateTime Created { get; set; }
    public IList<Port> Ports { get; set; } = [];

    public string PortDisplay => Ports.Count == 0
        ? "-"
        : string.Join(", ", Ports
            .Where(p => p.PublicPort > 0)
            .Select(p => $"{p.PublicPort}:{p.PrivatePort}/{p.Type}"));

    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);
    public bool IsExited => State.Equals("exited", StringComparison.OrdinalIgnoreCase);

    public string StateClass => State.ToLowerInvariant() switch
    {
        "running" => "status-running",
        "exited" => "status-exited",
        "paused" => "status-paused",
        "restarting" => "status-restarting",
        "created" => "status-created",
        _ => "status-unknown"
    };

    public static ContainerViewModel FromApi(ContainerListResponse c)
    {
        var labels = c.Labels ?? new Dictionary<string, string>();
        return new ContainerViewModel
        {
            Id = c.ID,
            Name = c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12],
            Image = c.Image,
            State = c.State,
            Status = c.Status,
            ComposeProject = labels.TryGetValue("com.docker.compose.project", out var proj) ? proj : null,
            ComposeService = labels.TryGetValue("com.docker.compose.service", out var svc) ? svc : null,
            Created = c.Created,
            Ports = c.Ports
        };
    }
}
