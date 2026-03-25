namespace Kontainr.Models;

public static class AppTemplates
{
    public static readonly List<AppTemplate> Templates =
    [
        new("Nginx", "Web Server", "nginx:alpine", [("80", "80")], [], "unless-stopped"),
        new("PostgreSQL", "Database", "postgres:16-alpine", [("5432", "5432")],
            [("POSTGRES_PASSWORD", "changeme"), ("POSTGRES_DB", "mydb")], "unless-stopped"),
        new("Redis", "Cache", "redis:7-alpine", [("6379", "6379")], [], "unless-stopped"),
        new("MySQL", "Database", "mysql:8", [("3306", "3306")],
            [("MYSQL_ROOT_PASSWORD", "changeme"), ("MYSQL_DATABASE", "mydb")], "unless-stopped"),
        new("MongoDB", "Database", "mongo:7", [("27017", "27017")], [], "unless-stopped"),
        new("MariaDB", "Database", "mariadb:11", [("3306", "3306")],
            [("MARIADB_ROOT_PASSWORD", "changeme"), ("MARIADB_DATABASE", "mydb")], "unless-stopped"),
        new("Adminer", "DB Admin UI", "adminer:latest", [("8080", "8080")], [], "unless-stopped"),
        new("Portainer Agent", "Docker Agent", "portainer/agent:latest", [("9001", "9001")], [], "unless-stopped"),
        new("Grafana", "Monitoring Dashboard", "grafana/grafana:latest", [("3000", "3000")], [], "unless-stopped"),
        new("Prometheus", "Metrics Collector", "prom/prometheus:latest", [("9090", "9090")], [], "unless-stopped"),
        new("Caddy", "Reverse Proxy", "caddy:2-alpine", [("80", "80"), ("443", "443")], [], "unless-stopped"),
        new("Traefik", "Reverse Proxy", "traefik:v3.0", [("80", "80"), ("8080", "8080")], [], "unless-stopped"),
        new("Pi-hole", "DNS Ad Blocker", "pihole/pihole:latest", [("53", "53"), ("80", "80")],
            [("WEBPASSWORD", "changeme")], "unless-stopped"),
        new("Gitea", "Git Server", "gitea/gitea:latest", [("3000", "3000"), ("22", "2222")], [], "unless-stopped"),
        new("Watchtower", "Auto Updater", "containrrr/watchtower:latest", [], [], "unless-stopped"),
        new("Uptime Kuma", "Uptime Monitor", "louislam/uptime-kuma:latest", [("3001", "3001")], [], "unless-stopped"),
        new("Heimdall", "App Dashboard", "lscr.io/linuxserver/heimdall:latest", [("80", "80"), ("443", "443")], [], "unless-stopped"),
        new("Mealie", "Recipe Manager", "hkotel/mealie:latest", [("9000", "9000")], [], "unless-stopped"),
        new("Vaultwarden", "Password Manager", "vaultwarden/server:latest", [("80", "80")], [], "unless-stopped"),
        new("FileBrowser", "File Manager", "filebrowser/filebrowser:latest", [("8080", "8080")], [], "unless-stopped"),
    ];
}

public record AppTemplate(
    string Name,
    string Category,
    string Image,
    List<(string Host, string Container)> Ports,
    List<(string Key, string Value)> Env,
    string RestartPolicy
);
