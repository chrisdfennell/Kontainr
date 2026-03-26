using System.Net.Http.Headers;
using System.Text.Json;
using Kontainr.Models;

namespace Kontainr.Services;

public class RegistryService(SshSettingsService settings)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<bool> TestConnectionAsync(RegistryConfig config)
    {
        try
        {
            var url = GetBaseUrl(config);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/v2/");
            await AddAuthAsync(request, config);
            var response = await Http.SendAsync(request);

            // Docker Hub returns 401 without scope, but that still means it's reachable
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListRepositoriesAsync(RegistryConfig config, int limit = 100)
    {
        if (config.Type == RegistryType.DockerHub)
            return await ListDockerHubReposAsync(config, limit);

        var url = GetBaseUrl(config);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/v2/_catalog?n={limit}");
        await AddAuthAsync(request, config);
        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var repos = new List<string>();
        if (doc.RootElement.TryGetProperty("repositories", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
                repos.Add(item.GetString() ?? "");
        }
        return repos;
    }

    public async Task<List<string>> ListTagsAsync(RegistryConfig config, string repository)
    {
        var url = GetBaseUrl(config);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/v2/{repository}/tags/list");
        await AddAuthAsync(request, config, repository);
        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var tags = new List<string>();
        if (doc.RootElement.TryGetProperty("tags", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                tags.Add(item.GetString() ?? "");
        }
        return tags;
    }

    public async Task<RegistryTagDetail> GetTagDetailAsync(RegistryConfig config, string repository, string tag)
    {
        var url = GetBaseUrl(config);
        var request = new HttpRequestMessage(HttpMethod.Get, $"{url}/v2/{repository}/manifests/{tag}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
        await AddAuthAsync(request, config, repository);

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var digest = response.Headers.Contains("Docker-Content-Digest")
            ? response.Headers.GetValues("Docker-Content-Digest").FirstOrDefault() ?? ""
            : "";

        long totalSize = 0;
        if (doc.RootElement.TryGetProperty("layers", out var layers))
        {
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.TryGetProperty("size", out var s))
                    totalSize += s.GetInt64();
            }
        }

        return new RegistryTagDetail
        {
            Tag = tag,
            Digest = digest.Length > 19 ? digest[..19] + "..." : digest,
            Size = totalSize
        };
    }

    private async Task<List<string>> ListDockerHubReposAsync(RegistryConfig config, int limit)
    {
        var ns = string.IsNullOrWhiteSpace(config.Namespace) ? config.Username : config.Namespace;
        if (string.IsNullOrWhiteSpace(ns)) return [];

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://hub.docker.com/v2/repositories/{ns}/?page_size={limit}");
        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var repos = new List<string>();
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            foreach (var item in results.EnumerateArray())
            {
                var name = item.GetProperty("name").GetString() ?? "";
                repos.Add($"{ns}/{name}");
            }
        }
        return repos;
    }

    private static string GetBaseUrl(RegistryConfig config)
    {
        return config.Type switch
        {
            RegistryType.DockerHub => "https://registry-1.docker.io",
            RegistryType.GitLab => string.IsNullOrWhiteSpace(config.Url) ? "https://registry.gitlab.com" : config.Url.TrimEnd('/'),
            _ => config.Url.TrimEnd('/')
        };
    }

    private async Task AddAuthAsync(HttpRequestMessage request, RegistryConfig config, string? repository = null)
    {
        var password = string.IsNullOrEmpty(config.EncryptedPassword) ? "" : settings.DecryptPassword(config.EncryptedPassword);

        if (config.Type == RegistryType.DockerHub)
        {
            // Docker Hub requires token auth
            var scope = repository is not null ? $"repository:{repository}:pull" : "registry:catalog:*";
            var tokenUrl = $"https://auth.docker.io/token?service=registry.docker.io&scope={scope}";
            var tokenRequest = new HttpRequestMessage(HttpMethod.Get, tokenUrl);

            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(password))
            {
                var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{config.Username}:{password}"));
                tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
            }

            var tokenResponse = await Http.SendAsync(tokenRequest);
            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenDoc = JsonDocument.Parse(tokenJson);
                if (tokenDoc.RootElement.TryGetProperty("token", out var tokenProp))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenProp.GetString());
                    return;
                }
            }
        }

        // Basic auth for all other registries
        if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(password))
        {
            var creds = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{config.Username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        }
    }
}
