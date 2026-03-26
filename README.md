<p align="center">
  <img src="https://raw.githubusercontent.com/chrisdfennell/Kontainr/refs/heads/dev/Kontainr/wwwroot/favicon.svg" width="80" alt="Kontainr logo"/>
  <br><br>
  <img src="https://raw.githubusercontent.com/chrisdfennell/Kontainr/refs/heads/dev/Kontainr/wwwroot/title.svg" width="300" alt="Kontainr"/>
  <br>
  <em>A lightweight Docker dashboard for managing containers, logs, and services.</em>
</p>

<p align="center">
  <a href="https://github.com/chrisdfennell/Kontainr/actions"><img src="https://github.com/chrisdfennell/Kontainr/actions/workflows/docker-publish.yml/badge.svg" alt="CI/CD"></a>
  <a href="https://hub.docker.com/r/fennch/kontainr"><img src="https://img.shields.io/docker/pulls/fennch/kontainr" alt="Docker Pulls"></a>
  <a href="https://hub.docker.com/r/fennch/kontainr"><img src="https://img.shields.io/docker/image-size/fennch/kontainr/latest" alt="Docker Image Size"></a>
  <a href="https://hub.docker.com/r/fennch/kontainr"><img src="https://img.shields.io/docker/v/fennch/kontainr?sort=semver" alt="Docker Version"></a>
  <a href="https://github.com/chrisdfennell/Kontainr/releases"><img src="https://img.shields.io/github/v/release/chrisdfennell/Kontainr" alt="Release"></a>
  <a href="https://github.com/chrisdfennell/Kontainr"><img src="https://img.shields.io/github/stars/chrisdfennell/Kontainr?style=flat" alt="Stars"></a>
  <a href="https://github.com/chrisdfennell/Kontainr/issues"><img src="https://img.shields.io/github/issues/chrisdfennell/Kontainr" alt="Issues"></a>
  <a href="https://github.com/chrisdfennell/Kontainr/commits/main"><img src="https://img.shields.io/github/last-commit/chrisdfennell/Kontainr" alt="Last Commit"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/chrisdfennell/Kontainr" alt="License"></a>
</p>

---

<p align="center">
  <img src="https://raw.githubusercontent.com/chrisdfennell/Kontainr/refs/heads/dev/Kontainr/wwwroot/screenshot1.png" width="900" alt="Kontainr Dashboard"/>
</p>

---

## Features

### Container Management
- **Dashboard** — overview of running/stopped containers, images, volumes with live CPU/RAM stats
- **Crash/Restart Alerts** — automatic detection of crashed or restart-looping containers with webhook notifications (Discord, Slack, or generic HTTP)
- **Favorites** — pin containers to the top of the dashboard for quick access
- **Start, Stop, Restart, Remove** — all with confirmation dialogs and toast notifications
- **Container Creation Wizard** — pull image, configure ports, volumes, env vars, restart policy, network, CPU/memory limits
- **Container Config Editor** — edit env vars, ports, restart policy, network and recreate with new config
- **Container Clone** — duplicate any container's config as a new container with one click
- **One-Click Update** — pull latest image and recreate container with the same configuration
- **Self-Update** — Kontainr can update its own container via a temporary updater sidecar, with automatic page reload
- **Update Checker** — scan all containers for newer registry images, with "self" badge identifying Kontainr's own container
- **Scheduled Restarts** — cron-style scheduled container restarts (daily, weekly, or interval-based)
- **Docker Compose Deploy** — upload or paste a `docker-compose.yml` and deploy stacks from the UI
- **Docker Compose Grouping** — containers grouped by project with bulk start/stop/restart
- **Health Check Badges** — healthy/unhealthy/starting indicators on containers

### Monitoring
- **CPU/RAM Sparkline Graphs** — resource usage over time on container detail pages
- **Live Log Streaming** — real-time `docker logs -f` with search/filter
- **Log Export** — download container logs as a text file
- **Auto-Refresh** — configurable 3s/5s/10s/30s polling with visual indicator
- **Clickable Port Links** — port mappings link directly to the service, configurable host URL

### Terminals
- **Interactive Container Shell** — full xterm.js TTY terminal into any running container
- **Interactive SSH Terminal** — full xterm.js TTY terminal to remote servers
- **Terminal Hub** — all SSH connections and running containers in one place
- **Init Commands** — configurable startup commands to escape login menus (e.g. QNAP `Q, Y`)

### Resource Management
- **Images** — list, pull, remove, prune dangling images
- **Image Inspector** — view layers, entrypoint, env vars, exposed ports, volumes, architecture
- **Volumes** — list, create, remove, prune unused volumes
- **Networks** — list, create (bridge/host/overlay/macvlan), remove, prune unused networks
- **Network Topology** — interactive Cytoscape.js graph showing containers connected to their networks with port mappings, color-coded edges, hover highlighting, and multiple layout options
- **System Info** — Docker version, CPU/RAM, storage driver, kernel, full system prune

### App Templates
- **55+ Pre-built Templates** — one-click deploy for Nginx, PostgreSQL, Redis, Grafana, Pi-hole, Jellyfin, the full *arr stack, and more
- **12 Categories** — web servers, databases, monitoring, media, dev tools, security, networking, and more
- **Configurable Deploy** — change container name, ports, and env vars before deploying

### Settings & Security
- **Basic Auth** — optional password protection via environment variables
- **SSH Connection Manager** — add, edit, test, delete connections with encrypted password storage
- **Webhook Notifications** — Discord, Slack, or generic HTTP alerts for container crashes
- **Configurable Host URL** — port links use your NAS hostname instead of localhost
- **Persistent Data** — settings and encryption keys survive container rebuilds via volume mount
- **Backup & Restore** — export/import all settings as JSON
- **Global Search** — search containers, images, volumes, and networks from any page
- **Audit Log** — tracks all actions with timestamps
- **Dark/Light Theme** — toggle between dark and light mode, persisted to settings

## Quick Start

```bash
docker run -d \
  --name kontainr \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v kontainr-data:/app/data \
  fennch/kontainr:latest
```

Then open `http://localhost:8080`.

### With Authentication

```bash
docker run -d \
  --name kontainr \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v kontainr-data:/app/data \
  -e Auth__Username=admin \
  -e Auth__Password=changeme \
  fennch/kontainr:latest
```

### Docker Compose

```yaml
services:
  kontainr:
    image: fennch/kontainr:latest
    ports:
      - "8080:8080"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - kontainr-data:/app/data
    environment:
      - Auth__Username=admin
      - Auth__Password=changeme
    restart: unless-stopped

volumes:
  kontainr-data:
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `Auth__Username` | Login username (optional) | *(none — no auth)* |
| `Auth__Password` | Login password (optional) | *(none — no auth)* |
| `KONTAINR_DATA` | Data directory for settings & keys | `/app/data` |

### Persistent Data

Mount a volume to `/app/data` to persist:
- SSH connection configs (passwords encrypted with ASP.NET Data Protection)
- Webhook configuration and scheduled restarts
- Host URL, theme, favorites, and app settings
- Encryption keys

### SSH Connections

1. Go to **Settings** in the sidebar
2. Click **Add Connection**
3. Enter host, port, username, password
4. Optionally add **Init Commands** (comma-separated) for servers with login menus (e.g. `Q, Y` for QNAP NAS)
5. Click **Test Connection** to verify, then **Save**
6. Go to **Terminal** in the sidebar and click **Connect**

### Webhook Notifications

1. Go to **Settings** > **Webhook Notifications**
2. Paste a Discord webhook URL, Slack webhook URL, or any HTTP endpoint
3. Enable notifications and choose alert types (crash, restart loop)
4. Kontainr auto-detects the URL format and sends rich embeds for Discord, formatted messages for Slack, or generic JSON for everything else

### Port Link Host URL

By default, clickable port links point to `http://localhost:{port}`. If Kontainr runs on a NAS or remote server, go to **Settings** and set the **Docker Host URL** to your server's hostname (e.g. `fennell-nas`).

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/)

### Run Locally

```bash
cd Kontainr
dotnet run
```

### Build Docker Image

```bash
docker build -t kontainr -f Kontainr/Dockerfile Kontainr/
```

## Tech Stack

- **Blazor Server** (.NET 10) — real-time interactive UI
- **Docker.DotNet** — Docker Engine API client
- **SSH.NET** — SSH client for remote terminal
- **xterm.js** — interactive terminal emulator
- **Cytoscape.js** — network topology graph visualization
- **ASP.NET Data Protection** — encrypted credential storage
- **Bootstrap 5** — base CSS with custom dark theme

## CI/CD

| Branch | Docker Hub Tags | Release |
|--------|----------------|---------|
| `dev` | `fennch/kontainr:dev` | No |
| `main` | `fennch/kontainr:latest` + `fennch/kontainr:X.X.X` | Yes — Git tag + GitHub Release |

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
