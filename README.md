<p align="center">
  <img src="Kontainr/wwwroot/favicon.svg" width="80" alt="Kontainr logo"/>
</p>

<h1 align="center">Kontainr</h1>

<p align="center">
  A lightweight Docker dashboard for managing containers, logs, and services.
</p>

<p align="center">
  <a href="https://github.com/chrisdfennell/Kontainr/actions"><img src="https://github.com/chrisdfennell/Kontainr/actions/workflows/docker-publish.yml/badge.svg" alt="CI/CD"></a>
  <a href="https://hub.docker.com/r/fennch/kontainr"><img src="https://img.shields.io/docker/pulls/fennch/kontainr" alt="Docker Pulls"></a>
  <a href="https://github.com/chrisdfennell/Kontainr/releases"><img src="https://img.shields.io/github/v/release/chrisdfennell/Kontainr" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/chrisdfennell/Kontainr" alt="License"></a>
</p>

---

## Features

### Container Management
- **Dashboard** — overview of running/stopped containers, images, volumes with live CPU/RAM stats
- **Start, Stop, Restart, Remove** — all with confirmation dialogs and toast notifications
- **Container Creation Wizard** — pull image, configure ports, volumes, env vars, restart policy, network
- **One-Click Update** — pull latest image and recreate container with the same configuration
- **Docker Compose Grouping** — containers grouped by project with bulk start/stop/restart
- **Health Check Badges** — healthy/unhealthy/starting indicators on containers

### Monitoring
- **CPU/RAM Sparkline Graphs** — resource usage over time on container detail pages
- **Live Log Streaming** — real-time `docker logs -f` with search/filter
- **Auto-Refresh** — configurable 3s/5s/10s/30s polling with visual indicator
- **Clickable Port Links** — port mappings link directly to the service, configurable host URL

### Terminals
- **Container Exec** — shell into any running container via `/bin/sh`
- **SSH Terminal** — connect to remote servers with saved, encrypted credentials
- **Init Commands** — configurable startup commands to escape login menus (e.g. QNAP `Q, Y`)
- **Command History** — arrow key navigation, auto-focus after execution

### Resource Management
- **Images** — list, pull, remove, prune dangling images
- **Volumes** — list, remove, prune unused volumes
- **Networks** — list, remove, prune unused networks
- **System Info** — Docker version, CPU/RAM, storage driver, kernel, full system prune

### Settings & Security
- **Basic Auth** — optional password protection via environment variables
- **SSH Connection Manager** — add, edit, test, delete connections with encrypted password storage
- **Configurable Host URL** — port links use your NAS hostname instead of localhost
- **Persistent Data** — settings and encryption keys survive container rebuilds via volume mount
- **Dark Theme** — GitHub-dark inspired UI with responsive sidebar

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
- Host URL and app settings
- Encryption keys

### SSH Connections

1. Go to **Settings** in the sidebar
2. Click **Add Connection**
3. Enter host, port, username, password
4. Optionally add **Init Commands** (comma-separated) for servers with login menus (e.g. `Q, Y` for QNAP NAS)
5. Click **Test Connection** to verify, then **Save**
6. Click **Connect** to open a terminal session

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
- **ASP.NET Data Protection** — encrypted credential storage
- **Bootstrap 5** — base CSS with custom dark theme

## CI/CD

| Branch | Docker Hub Tags | Release |
|--------|----------------|---------|
| `dev` | `fennch/kontainr:dev` | No |
| `main` | `fennch/kontainr:latest` + `fennch/kontainr:X.X.X` | Yes — Git tag + GitHub Release |

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
