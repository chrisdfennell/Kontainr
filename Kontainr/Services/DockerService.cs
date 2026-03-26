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

    // ── Interactive Exec ───────────────────────────────────────────

    public async Task<(string execId, MultiplexedStream stream)> CreateInteractiveExecAsync(string containerId)
    {
        var exec = await _client.Exec.ExecCreateContainerAsync(containerId, new ContainerExecCreateParameters
        {
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            Tty = true,
            Cmd = ["/bin/sh"],
        });

        var stream = await _client.Exec.StartAndAttachContainerExecAsync(exec.ID, true, default);
        return (exec.ID, stream);
    }

    public async Task ResizeExecAsync(string execId, uint rows, uint cols)
    {
        // Resize is best-effort — not all Docker API versions support it the same way
        try
        {
            await _client.Containers.ResizeContainerTtyAsync(execId, new ContainerResizeParameters
            {
                Height = rows,
                Width = cols
            }, CancellationToken.None);
        }
        catch { /* silently ignore resize failures */ }
    }

    // ── Container Archive (file browser) ────────────────────────

    public async Task<GetArchiveFromContainerResponse> GetArchiveAsync(string id, string path)
    {
        return await _client.Containers.GetArchiveFromContainerAsync(id,
            new GetArchiveFromContainerParameters { Path = path }, false);
    }

    public async Task ExtractArchiveAsync(string id, string path, Stream tarStream)
    {
        await _client.Containers.ExtractArchiveToContainerAsync(id,
            new ContainerPathStatParameters { Path = path }, tarStream);
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
        await PullImageAsync(image, tag, onProgress, null, ct);
    }

    public async Task PullImageAsync(string image, string tag, Action<string> onProgress, AuthConfig? auth, CancellationToken ct = default)
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
            auth, progress, ct);
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

    // ── Self-Update (for updating Kontainr's own container) ──────

    /// <summary>
    /// Detects if the given container ID matches the container Kontainr is running in.
    /// Inside Docker, the hostname is the short container ID.
    /// </summary>
    public bool IsSelf(string containerId) =>
        containerId.StartsWith(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Updates Kontainr's own container by spawning a temporary docker:cli container
    /// that performs the stop/remove/create/start cycle after a short delay.
    /// </summary>
    public async Task SelfUpdateAsync(string selfId, Action<string>? onProgress = null)
    {
        var inspect = await InspectContainerAsync(selfId);
        var name = inspect.Name.TrimStart('/');
        var image = inspect.Config.Image;

        // 1. Pull the latest image
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

        // 2. Pull docker:cli for the updater container
        onProgress?.Invoke("Preparing updater...");
        try
        {
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = "docker", Tag = "cli" },
                null, new Progress<JSONMessage>());
        }
        catch
        {
            onProgress?.Invoke("Failed to pull docker:cli image");
            throw;
        }

        // 3. Build the docker create command from the inspected config
        var args = new List<string>();

        // Name
        args.Add($"--name {name}");

        // Restart policy
        var restartKind = inspect.HostConfig.RestartPolicy?.Name;
        if (restartKind is not null && restartKind != RestartPolicyKind.Undefined && restartKind != RestartPolicyKind.No)
        {
            var rpStr = restartKind switch
            {
                RestartPolicyKind.Always => "always",
                RestartPolicyKind.UnlessStopped => "unless-stopped",
                RestartPolicyKind.OnFailure => "on-failure",
                _ => "no"
            };
            if (restartKind == RestartPolicyKind.OnFailure && inspect.HostConfig.RestartPolicy.MaximumRetryCount > 0)
                rpStr += $":{inspect.HostConfig.RestartPolicy.MaximumRetryCount}";
            args.Add($"--restart {rpStr}");
        }

        // Environment variables
        if (inspect.Config.Env is not null)
        {
            foreach (var env in inspect.Config.Env)
                args.Add($"-e '{env}'");
        }

        // Port bindings
        if (inspect.HostConfig.PortBindings is not null)
        {
            foreach (var (containerPort, bindings) in inspect.HostConfig.PortBindings)
            {
                if (bindings is null) continue;
                foreach (var b in bindings)
                {
                    var hostPart = string.IsNullOrEmpty(b.HostIP) ? "" : $"{b.HostIP}:";
                    args.Add($"-p {hostPart}{b.HostPort}:{containerPort}");
                }
            }
        }

        // Volume binds
        if (inspect.HostConfig.Binds is not null)
        {
            foreach (var bind in inspect.HostConfig.Binds)
                args.Add($"-v '{bind}'");
        }

        // Network mode
        if (!string.IsNullOrEmpty(inspect.HostConfig.NetworkMode) && inspect.HostConfig.NetworkMode != "default")
            args.Add($"--network {inspect.HostConfig.NetworkMode}");

        // Labels
        if (inspect.Config.Labels is not null)
        {
            foreach (var (key, value) in inspect.Config.Labels)
                args.Add($"--label '{key}={value}'");
        }

        // Resource limits
        if (inspect.HostConfig.NanoCPUs > 0)
            args.Add($"--cpus {inspect.HostConfig.NanoCPUs / 1_000_000_000.0}");
        if (inspect.HostConfig.Memory > 0)
            args.Add($"--memory {inspect.HostConfig.Memory}");

        var dockerArgs = string.Join(" ", args);

        // 4. Build the updater script
        var script = $"""
            echo 'Waiting for Kontainr to send response...'
            sleep 5
            echo 'Stopping {name}...'
            docker stop {selfId} -t 10
            echo 'Removing {name}...'
            docker rm {selfId}
            echo 'Creating new {name}...'
            docker create {dockerArgs} {image}
            echo 'Starting {name}...'
            docker start {name}
            echo 'Self-update complete!'
            """;

        // 5. Determine the Docker socket path for the updater container
        var socketBind = OperatingSystem.IsWindows()
            ? "//./pipe/docker_engine://./pipe/docker_engine"
            : "/var/run/docker.sock:/var/run/docker.sock";

        // 6. Create and start the updater container
        onProgress?.Invoke("Launching updater...");
        var updater = await _client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "docker:cli",
            Name = $"kontainr-updater-{DateTime.UtcNow:yyyyMMddHHmmss}",
            Cmd = ["sh", "-c", script],
            Labels = new Dictionary<string, string> { ["kontainr.updater"] = "true" },
            HostConfig = new HostConfig
            {
                Binds = [socketBind],
                AutoRemove = true
            }
        });

        await _client.Containers.StartContainerAsync(updater.ID, new ContainerStartParameters());

        onProgress?.Invoke("Update in progress — page will reload automatically...");
    }

    // ── Clone Container ────────────────────────────────────────────

    public async Task<string> CloneContainerAsync(string id, string newName)
    {
        var inspect = await InspectContainerAsync(id);

        var createParams = new CreateContainerParameters
        {
            Image = inspect.Config.Image,
            Name = newName,
            Env = inspect.Config.Env,
            Cmd = inspect.Config.Cmd,
            WorkingDir = inspect.Config.WorkingDir,
            Labels = inspect.Config.Labels,
            ExposedPorts = inspect.Config.ExposedPorts,
            HostConfig = new HostConfig
            {
                Binds = inspect.HostConfig.Binds,
                RestartPolicy = inspect.HostConfig.RestartPolicy,
                NetworkMode = inspect.HostConfig.NetworkMode,
                NanoCPUs = inspect.HostConfig.NanoCPUs,
                Memory = inspect.HostConfig.Memory,
                // Don't copy port bindings — they'd conflict
            }
        };

        var result = await CreateContainerAsync(createParams);
        return result.ID;
    }

    // ── Recreate with new config ──────────────────────────────────

    public async Task<string> RecreateWithConfigAsync(string id, IList<string>? env, IDictionary<string, IList<PortBinding>>? portBindings,
        IDictionary<string, EmptyStruct>? exposedPorts, IList<string>? binds, RestartPolicy? restartPolicy, string? networkMode = null, Action<string>? onProgress = null)
    {
        var inspect = await InspectContainerAsync(id);
        var wasRunning = inspect.State?.Running == true;
        var name = inspect.Name.TrimStart('/');

        onProgress?.Invoke("Stopping container...");
        if (wasRunning) await StopContainerAsync(id);

        onProgress?.Invoke("Removing container...");
        await RemoveContainerAsync(id, force: true);

        onProgress?.Invoke("Creating with new config...");
        var hostConfig = inspect.HostConfig;
        if (portBindings is not null) hostConfig.PortBindings = portBindings;
        if (binds is not null) hostConfig.Binds = binds;
        if (restartPolicy is not null) hostConfig.RestartPolicy = restartPolicy;
        if (networkMode is not null) hostConfig.NetworkMode = networkMode;

        var createParams = new CreateContainerParameters
        {
            Image = inspect.Config.Image,
            Name = name,
            Env = env ?? inspect.Config.Env,
            Cmd = inspect.Config.Cmd,
            WorkingDir = inspect.Config.WorkingDir,
            Labels = inspect.Config.Labels,
            ExposedPorts = exposedPorts ?? inspect.Config.ExposedPorts,
            HostConfig = hostConfig
        };

        var newContainer = await CreateContainerAsync(createParams);
        if (wasRunning)
        {
            onProgress?.Invoke("Starting container...");
            await StartContainerAsync(newContainer.ID);
        }
        onProgress?.Invoke("Done!");
        return newContainer.ID;
    }

    // ── Update Checker ──────────────────────────────────────────

    public async Task<ImageInspectResponse> InspectImageAsync(string name)
    {
        return await _client.Images.InspectImageAsync(name);
    }

    /// <summary>
    /// Check if a newer image exists on the registry by pulling and comparing digests.
    /// Returns true if an update is available.
    /// </summary>
    public async Task<bool> CheckImageUpdateAsync(string image)
    {
        try
        {
            var repo = image;
            var tag = "latest";
            if (image.Contains(':'))
            {
                var parts = image.Split(':', 2);
                repo = parts[0];
                tag = parts[1];
            }

            // Get local image ID
            ImageInspectResponse? localInspect;
            try { localInspect = await InspectImageAsync(image); }
            catch { return false; } // image not found locally

            var localId = localInspect.ID;

            // Pull to check for updates (Docker will skip if already up to date)
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = repo, Tag = tag },
                null, new Progress<JSONMessage>());

            // Re-inspect to see if the ID changed
            var afterInspect = await InspectImageAsync(image);
            return afterInspect.ID != localId;
        }
        catch
        {
            return false;
        }
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

    public async Task CreateVolumeAsync(string name)
    {
        await _client.Volumes.CreateAsync(new VolumesCreateParameters { Name = name });
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

    public async Task CreateNetworkAsync(string name, string driver = "bridge")
    {
        await _client.Networks.CreateNetworkAsync(new NetworksCreateParameters { Name = name, Driver = driver });
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
