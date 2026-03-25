using Docker.DotNet;
using Docker.DotNet.Models;
using Kontainr.Models;

namespace Kontainr.Services;

public class DockerService : IDisposable
{
    private readonly DockerClient _client;

    public DockerService()
    {
        if (OperatingSystem.IsWindows())
            _client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
        else
            _client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();
    }

    // ── Containers ───────────────────────────────────────────────

    public async Task<IList<ContainerListResponse>> GetContainersAsync(bool all = true)
    {
        return await _client.Containers.ListContainersAsync(new ContainersListParameters { All = all });
    }

    public async Task<ContainerInspectResponse> InspectContainerAsync(string id)
    {
        return await _client.Containers.InspectContainerAsync(id);
    }

    public async Task StartContainerAsync(string id)
    {
        await _client.Containers.StartContainerAsync(id, new ContainerStartParameters());
    }

    public async Task StopContainerAsync(string id)
    {
        await _client.Containers.StopContainerAsync(id, new ContainerStopParameters { WaitBeforeKillSeconds = 10 });
    }

    public async Task RestartContainerAsync(string id)
    {
        await _client.Containers.RestartContainerAsync(id, new ContainerRestartParameters { WaitBeforeKillSeconds = 10 });
    }

    public async Task RemoveContainerAsync(string id, bool force = false)
    {
        await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force });
    }

    public async Task<string> GetContainerLogsAsync(string id, int tailLines = 200)
    {
        var muxStream = await _client.Containers.GetContainerLogsAsync(id, false, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tailLines.ToString(),
            Timestamps = true
        });

        var memStream = new MemoryStream();
        await muxStream.CopyOutputToAsync(Stream.Null, memStream, memStream, CancellationToken.None);
        memStream.Position = 0;
        using var reader = new StreamReader(memStream);
        return await reader.ReadToEndAsync();
    }

    public async Task StreamContainerLogsAsync(string id, int tailLines, Action<string> onLine, CancellationToken ct)
    {
        var muxStream = await _client.Containers.GetContainerLogsAsync(id, true, new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Tail = tailLines.ToString(),
            Timestamps = true,
            Follow = true
        }, ct);

        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await muxStream.ReadOutputAsync(buffer, 0, buffer.Length, ct);
                if (result.Count == 0) break;
                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    onLine(line.TrimEnd());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) when (ct.IsCancellationRequested) { }
    }

    public async Task<ContainerStatsResponse?> GetContainerStatsOnceAsync(string id)
    {
        ContainerStatsResponse? result = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _client.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters { Stream = false },
                new Progress<ContainerStatsResponse>(stats => result = stats), cts.Token);
        }
        catch { }
        return result;
    }

    // ── Exec ─────────────────────────────────────────────────────

    public async Task<string> ExecContainerAsync(string id, string command, CancellationToken ct = default)
    {
        var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(id, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            Cmd = ["/bin/sh", "-c", command],
            Tty = false
        }, ct);

        var muxStream = await _client.Exec.StartAndAttachContainerExecAsync(execCreateResponse.ID, false, ct);
        var (stdout, stderr) = await muxStream.ReadOutputToEndAsync(ct);
        return string.IsNullOrEmpty(stderr) ? stdout : $"{stdout}\n{stderr}";
    }

    // ── Images ───────────────────────────────────────────────────

    public async Task<IList<ImagesListResponse>> GetImagesAsync()
    {
        return await _client.Images.ListImagesAsync(new ImagesListParameters { All = false });
    }

    public async Task RemoveImageAsync(string id, bool force = false)
    {
        await _client.Images.DeleteImageAsync(id, new ImageDeleteParameters { Force = force });
    }

    public async Task PullImageAsync(string image, string tag, Action<string> onProgress, CancellationToken ct = default)
    {
        var progress = new Progress<JSONMessage>(msg =>
        {
            var status = msg.Status ?? "";
            if (!string.IsNullOrEmpty(msg.ProgressMessage))
                status += $" {msg.ProgressMessage}";
            onProgress(status);
        });

        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image, Tag = tag },
            null, progress, ct);
    }

    // ── Create Container ──────────────────────────────────────────

    public async Task<CreateContainerResponse> CreateContainerAsync(CreateContainerParameters parameters)
    {
        return await _client.Containers.CreateContainerAsync(parameters);
    }

    // ── Recreate Container (pull + recreate with same config) ───

    public async Task<string> RecreateContainerAsync(string id, Action<string>? onProgress = null)
    {
        var inspect = await InspectContainerAsync(id);
        var wasRunning = inspect.State?.Running == true;
        var name = inspect.Name.TrimStart('/');
        var image = inspect.Config.Image;

        // Pull latest image
        onProgress?.Invoke($"Pulling {image}...");
        var tag = "latest";
        var repo = image;
        if (image.Contains(':'))
        {
            var parts = image.Split(':', 2);
            repo = parts[0];
            tag = parts[1];
        }

        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = repo, Tag = tag },
            null,
            new Progress<JSONMessage>(msg =>
            {
                if (!string.IsNullOrEmpty(msg.Status))
                    onProgress?.Invoke(msg.Status);
            }));

        // Stop and remove old container
        onProgress?.Invoke("Stopping old container...");
        if (wasRunning)
            await StopContainerAsync(id);

        onProgress?.Invoke("Removing old container...");
        await RemoveContainerAsync(id, force: true);

        // Recreate with same config
        onProgress?.Invoke("Creating new container...");
        var createParams = new CreateContainerParameters
        {
            Image = image,
            Name = name,
            Env = inspect.Config.Env,
            Cmd = inspect.Config.Cmd,
            WorkingDir = inspect.Config.WorkingDir,
            Labels = inspect.Config.Labels,
            ExposedPorts = inspect.Config.ExposedPorts,
            HostConfig = inspect.HostConfig
        };

        var newContainer = await CreateContainerAsync(createParams);

        // Start if it was running before
        if (wasRunning)
        {
            onProgress?.Invoke("Starting new container...");
            await StartContainerAsync(newContainer.ID);
        }

        onProgress?.Invoke("Done!");
        return newContainer.ID;
    }

    // ── Prune ────────────────────────────────────────────────────

    public async Task<ContainersPruneResponse> PruneContainersAsync()
    {
        return await _client.Containers.PruneContainersAsync();
    }

    public async Task<ImagesPruneResponse> PruneImagesAsync()
    {
        return await _client.Images.PruneImagesAsync(new ImagesPruneParameters());
    }

    public async Task<VolumesPruneResponse> PruneVolumesAsync()
    {
        return await _client.Volumes.PruneAsync();
    }

    public async Task<NetworksPruneResponse> PruneNetworksAsync()
    {
        return await _client.Networks.PruneNetworksAsync();
    }

    // ── Volumes ──────────────────────────────────────────────────

    public async Task<VolumesListResponse> GetVolumesAsync()
    {
        return await _client.Volumes.ListAsync();
    }

    public async Task RemoveVolumeAsync(string name, bool force = false)
    {
        await _client.Volumes.RemoveAsync(name, force);
    }

    // ── Networks ─────────────────────────────────────────────────

    public async Task<IList<NetworkResponse>> GetNetworksAsync()
    {
        return await _client.Networks.ListNetworksAsync();
    }

    public async Task RemoveNetworkAsync(string id)
    {
        await _client.Networks.DeleteNetworkAsync(id);
    }

    // ── System ───────────────────────────────────────────────────

    public async Task<SystemInfoResponse> GetSystemInfoAsync()
    {
        return await _client.System.GetSystemInfoAsync();
    }

    public async Task<VersionResponse> GetVersionAsync()
    {
        return await _client.System.GetVersionAsync();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
