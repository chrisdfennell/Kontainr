<p align="center">
  <img src="Kontainr/wwwroot/favicon.svg" width="80" alt="Kontainr logo"/>
</p>

<h1 align="center">Kontainr</h1>

<p align="center">
  A lightweight Docker dashboard for managing containers, viewing logs, and running commands — built with Blazor Server and .NET 10.
</p>

<p align="center">
  <a href="https://github.com/chrisdfennell/Kontainr/actions"><img src="https://github.com/chrisdfennell/Kontainr/actions/workflows/docker-publish.yml/badge.svg" alt="CI/CD"></a>
  <a href="https://hub.docker.com/r/fennch/kontainr"><img src="https://img.shields.io/docker/pulls/fennch/kontainr" alt="Docker Pulls"></a>
  <a href="https://github.com/chrisdfennell/Kontainr/releases"><img src="https://img.shields.io/github/v/release/chrisdfennell/Kontainr" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/chrisdfennell/Kontainr" alt="License"></a>
</p>

---

## Features

- **Dashboard** — overview of running/stopped containers, images, volumes with live CPU/RAM stats
- **Container Management** — start, stop, restart, remove containers with confirmation dialogs
- **Live Log Streaming** — real-time `docker logs -f` with search/filter
- **Container Terminal** — exec into any running container via `/bin/sh`
- **SSH Terminal** — connect to remote servers with saved, encrypted credentials and configurable init commands (e.g. QNAP menu escape)
- **Image Management** — list, pull, remove images with prune support
- **Volumes & Networks** — view and manage with prune operations
- **Docker Compose Grouping** — containers grouped by Compose project with bulk start/stop/restart
- **Health Check Display** — healthy/unhealthy/starting badges on containers
- **Clickable Port Links** — port mappings link directly to the service, configurable host URL
- **System Info** — Docker version, CPU/RAM, storage driver, full system prune
- **Toast Notifications** — success/error feedback on all actions
- **Basic Auth** — optional password protection via environment variables
- **Dark Theme** — GitHub-dark inspired UI with responsive sidebar
- **Auto-Refresh** — configurable 3s/5s/10s/30s polling with visual indicator
- **Settings Page** — configure SSH connections, host URL, all persisted to a Docker volume

## Quick Start

```bash
docker run -d \
  --name kontainr \
  -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v kontainr-data:/app/data \
  fennch/kontainr:dev
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
  fennch/kontainr:dev
```

### Docker Compose

```yaml
services:
  kontainr:
    image: fennch/kontainr:dev
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

## Screenshots

| Dashboard | Containers | Terminal |
|-----------|-----------|----------|
| Live stats, auto-refresh | Compose grouping, bulk actions | SSH into remote servers |

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

Every push to `dev` triggers:
1. Docker image build and push to `fennch/kontainr:dev` + versioned tag
2. Auto-incrementing semantic version tag
3. GitHub Release with changelog

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
