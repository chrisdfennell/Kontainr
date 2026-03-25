namespace Kontainr.Models;

public static class AppTemplates
{
    public static readonly List<AppTemplate> Templates =
    [
        // Web Servers
        new("Nginx", "Web Server", "nginx:alpine", [("80", "80")], [], "unless-stopped"),
        new("Apache", "Web Server", "httpd:alpine", [("80", "80")], [], "unless-stopped"),
        new("Caddy", "Web Server / Reverse Proxy", "caddy:2-alpine", [("80", "80"), ("443", "443")], [], "unless-stopped"),

        // Reverse Proxies
        new("Traefik", "Reverse Proxy", "traefik:v3.0", [("80", "80"), ("8080", "8080")], [], "unless-stopped"),
        new("Nginx Proxy Manager", "Reverse Proxy UI", "jc21/nginx-proxy-manager:latest", [("80", "80"), ("81", "81"), ("443", "443")], [], "unless-stopped"),

        // Databases
        new("PostgreSQL", "Database", "postgres:16-alpine", [("5432", "5432")],
            [("POSTGRES_PASSWORD", "changeme"), ("POSTGRES_DB", "mydb")], "unless-stopped"),
        new("MySQL", "Database", "mysql:8", [("3306", "3306")],
            [("MYSQL_ROOT_PASSWORD", "changeme"), ("MYSQL_DATABASE", "mydb")], "unless-stopped"),
        new("MariaDB", "Database", "mariadb:11", [("3306", "3306")],
            [("MARIADB_ROOT_PASSWORD", "changeme"), ("MARIADB_DATABASE", "mydb")], "unless-stopped"),
        new("MongoDB", "Database", "mongo:7", [("27017", "27017")], [], "unless-stopped"),
        new("Redis", "Cache / Database", "redis:7-alpine", [("6379", "6379")], [], "unless-stopped"),
        new("InfluxDB", "Time Series DB", "influxdb:2", [("8086", "8086")], [], "unless-stopped"),
        new("CouchDB", "Database", "couchdb:3", [("5984", "5984")],
            [("COUCHDB_USER", "admin"), ("COUCHDB_PASSWORD", "changeme")], "unless-stopped"),

        // DB Admin
        new("Adminer", "DB Admin UI", "adminer:latest", [("8080", "8080")], [], "unless-stopped"),
        new("pgAdmin", "PostgreSQL Admin", "dpage/pgadmin4:latest", [("5050", "80")],
            [("PGADMIN_DEFAULT_EMAIL", "admin@local.dev"), ("PGADMIN_DEFAULT_PASSWORD", "changeme")], "unless-stopped"),
        new("phpMyAdmin", "MySQL Admin", "phpmyadmin:latest", [("8081", "80")],
            [("PMA_ARBITRARY", "1")], "unless-stopped"),
        new("Mongo Express", "MongoDB Admin", "mongo-express:latest", [("8081", "8081")],
            [("ME_CONFIG_BASICAUTH_USERNAME", "admin"), ("ME_CONFIG_BASICAUTH_PASSWORD", "changeme")], "unless-stopped"),

        // Monitoring
        new("Grafana", "Monitoring Dashboard", "grafana/grafana:latest", [("3000", "3000")], [], "unless-stopped"),
        new("Prometheus", "Metrics Collector", "prom/prometheus:latest", [("9090", "9090")], [], "unless-stopped"),
        new("Uptime Kuma", "Uptime Monitor", "louislam/uptime-kuma:latest", [("3001", "3001")], [], "unless-stopped"),
        new("Netdata", "System Monitor", "netdata/netdata:latest", [("19999", "19999")], [], "unless-stopped"),
        new("cAdvisor", "Container Monitor", "gcr.io/cadvisor/cadvisor:latest", [("8080", "8080")], [], "unless-stopped"),

        // Home / Self-Hosted
        new("Home Assistant", "Home Automation", "homeassistant/home-assistant:stable", [("8123", "8123")], [], "unless-stopped"),
        new("Heimdall", "App Dashboard", "lscr.io/linuxserver/heimdall:latest", [("80", "80"), ("443", "443")], [], "unless-stopped"),
        new("Homepage", "App Dashboard", "ghcr.io/gethomepage/homepage:latest", [("3000", "3000")], [], "unless-stopped"),
        new("Homarr", "App Dashboard", "ghcr.io/ajnart/homarr:latest", [("7575", "7575")], [], "unless-stopped"),
        new("Mealie", "Recipe Manager", "hkotel/mealie:latest", [("9000", "9000")], [], "unless-stopped"),
        new("Paperless-ngx", "Document Manager", "ghcr.io/paperless-ngx/paperless-ngx:latest", [("8000", "8000")], [], "unless-stopped"),
        new("Bookstack", "Wiki / Documentation", "lscr.io/linuxserver/bookstack:latest", [("6875", "80")],
            [("APP_URL", "http://localhost:6875"), ("DB_HOST", "bookstack-db"), ("DB_DATABASE", "bookstack"), ("DB_USERNAME", "bookstack"), ("DB_PASSWORD", "changeme")], "unless-stopped"),

        // Media
        new("Jellyfin", "Media Server", "jellyfin/jellyfin:latest", [("8096", "8096")], [], "unless-stopped"),
        new("Plex", "Media Server", "lscr.io/linuxserver/plex:latest", [("32400", "32400")], [], "unless-stopped"),
        // *arr Stack
        new("Sonarr", "*arr Stack", "lscr.io/linuxserver/sonarr:latest", [("8989", "8989")], [], "unless-stopped"),
        new("Radarr", "*arr Stack", "lscr.io/linuxserver/radarr:latest", [("7878", "7878")], [], "unless-stopped"),
        new("Lidarr", "*arr Stack", "lscr.io/linuxserver/lidarr:latest", [("8686", "8686")], [], "unless-stopped"),
        new("Readarr", "*arr Stack", "lscr.io/linuxserver/readarr:develop", [("8787", "8787")], [], "unless-stopped"),
        new("Whisparr", "*arr Stack", "ghcr.io/hotio/whisparr:latest", [("6969", "6969")], [], "unless-stopped"),
        new("Prowlarr", "*arr Stack", "lscr.io/linuxserver/prowlarr:latest", [("9696", "9696")], [], "unless-stopped"),
        new("Bazarr", "*arr Stack", "lscr.io/linuxserver/bazarr:latest", [("6767", "6767")], [], "unless-stopped"),
        new("Overseerr", "*arr Stack", "lscr.io/linuxserver/overseerr:latest", [("5055", "5055")], [], "unless-stopped"),
        new("Requestrr", "*arr Stack", "thomst08/requestrr:latest", [("4545", "4545")], [], "unless-stopped"),
        new("Tautulli", "*arr Stack", "lscr.io/linuxserver/tautulli:latest", [("8181", "8181")], [], "unless-stopped"),
        new("Flaresolverr", "*arr Stack", "ghcr.io/flaresolverr/flaresolverr:latest", [("8191", "8191")], [], "unless-stopped"),
        new("Recyclarr", "*arr Stack", "ghcr.io/recyclarr/recyclarr:latest", [], [], "unless-stopped"),
        new("Autobrr", "*arr Stack", "ghcr.io/autobrr/autobrr:latest", [("7474", "7474")], [], "unless-stopped"),
        new("Unpackerr", "*arr Stack", "ghcr.io/unpackerr/unpackerr:latest", [], [], "unless-stopped"),

        // Download Clients
        new("qBittorrent", "Download Client", "lscr.io/linuxserver/qbittorrent:latest", [("8080", "8080"), ("6881", "6881")], [], "unless-stopped"),
        new("Transmission", "Download Client", "lscr.io/linuxserver/transmission:latest", [("9091", "9091"), ("51413", "51413")], [], "unless-stopped"),
        new("Deluge", "Download Client", "lscr.io/linuxserver/deluge:latest", [("8112", "8112"), ("6881", "6881")], [], "unless-stopped"),
        new("SABnzbd", "Download Client", "lscr.io/linuxserver/sabnzbd:latest", [("8080", "8080")], [], "unless-stopped"),
        new("NZBGet", "Download Client", "lscr.io/linuxserver/nzbget:latest", [("6789", "6789")], [], "unless-stopped"),

        // Dev Tools
        new("Gitea", "Git Server", "gitea/gitea:latest", [("3000", "3000"), ("2222", "22")], [], "unless-stopped"),
        new("GitLab", "Git Server + CI/CD", "gitlab/gitlab-ce:latest", [("8929", "80"), ("2224", "22")], [], "unless-stopped"),
        new("Code Server", "VS Code in Browser", "lscr.io/linuxserver/code-server:latest", [("8443", "8443")], [], "unless-stopped"),
        new("Drone", "CI/CD", "drone/drone:latest", [("8000", "80")], [], "unless-stopped"),
        new("Registry", "Docker Registry", "registry:2", [("5000", "5000")], [], "unless-stopped"),

        // Security / Auth
        new("Vaultwarden", "Password Manager", "vaultwarden/server:latest", [("8080", "80")], [], "unless-stopped"),
        new("Authelia", "SSO / 2FA", "authelia/authelia:latest", [("9091", "9091")], [], "unless-stopped"),
        new("Keycloak", "Identity Provider", "quay.io/keycloak/keycloak:latest", [("8080", "8080")],
            [("KEYCLOAK_ADMIN", "admin"), ("KEYCLOAK_ADMIN_PASSWORD", "changeme")], "unless-stopped"),

        // Networking
        new("Pi-hole", "DNS Ad Blocker", "pihole/pihole:latest", [("53", "53"), ("8080", "80")],
            [("WEBPASSWORD", "changeme")], "unless-stopped"),
        new("WireGuard", "VPN Server", "lscr.io/linuxserver/wireguard:latest", [("51820", "51820")], [], "unless-stopped"),
        new("Tailscale", "Mesh VPN", "tailscale/tailscale:latest", [], [], "unless-stopped"),

        // Utilities
        new("Watchtower", "Auto Updater", "containrrr/watchtower:latest", [], [], "unless-stopped"),
        new("FileBrowser", "File Manager", "filebrowser/filebrowser:latest", [("8080", "80")], [], "unless-stopped"),
        new("Duplicati", "Backup", "lscr.io/linuxserver/duplicati:latest", [("8200", "8200")], [], "unless-stopped"),
        new("Portainer Agent", "Docker Agent", "portainer/agent:latest", [("9001", "9001")], [], "unless-stopped"),
        new("Dozzle", "Container Log Viewer", "amir20/dozzle:latest", [("8080", "8080")], [], "unless-stopped"),
        new("Whoami", "Test / Debug", "traefik/whoami:latest", [("8080", "80")], [], "unless-stopped"),

        // Communication
        new("Rocket.Chat", "Team Chat", "rocket.chat:latest", [("3000", "3000")], [], "unless-stopped"),
        new("Mattermost", "Team Chat", "mattermost/mattermost-team-edition:latest", [("8065", "8065")], [], "unless-stopped"),
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
