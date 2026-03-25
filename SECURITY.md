# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| dev     | Yes       |
| < dev   | No        |

## Reporting a Vulnerability

If you discover a security vulnerability in Kontainr, please report it responsibly:

1. **Do not** open a public issue
2. Open a private vulnerability report via [GitHub Security Advisories](https://github.com/chrisdfennell/Kontainr/security/advisories/new)
3. Or contact the maintainer directly

## Security Considerations

- **Docker Socket Access**: Kontainr requires access to the Docker socket (`/var/run/docker.sock`), which grants full control over Docker. Only run Kontainr in trusted environments.
- **SSH Credentials**: Passwords are encrypted at rest using ASP.NET Data Protection. The encryption keys are stored in the `/app/data/keys` directory — mount this as a persistent volume.
- **Basic Auth**: When enabled, credentials are transmitted via HTTP Basic Auth. Use a reverse proxy with TLS (e.g. Traefik, Caddy, nginx) for production deployments.
- **No Default Credentials**: Authentication is disabled by default. Enable it by setting `Auth__Username` and `Auth__Password` environment variables.
