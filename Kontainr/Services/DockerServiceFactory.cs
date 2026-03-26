namespace Kontainr.Services;

public class DockerServiceFactory
{
    private readonly DockerHostManager _hostManager;

    public DockerServiceFactory(DockerHostManager hostManager)
    {
        _hostManager = hostManager;
    }

    public DockerService GetService(string hostId)
    {
        var client = _hostManager.GetClient(hostId);
        var config = _hostManager.GetHostConfig(hostId);
        return new DockerService(client, hostId, config.Name);
    }

    public DockerService GetLocalService() => GetService("local");

    public IReadOnlyList<string> GetAllHostIds() => _hostManager.GetAllHostIds();

    public Models.DockerHostConfig GetHostConfig(string hostId) => _hostManager.GetHostConfig(hostId);

    public IReadOnlyList<Models.DockerHostConfig> GetAllHostConfigs() => _hostManager.GetAllHostConfigs();
}
